namespace MarkDrip.Parser;

/// <summary>
/// 分段存储的字符缓冲区，支持零分配前缀匹配。
///
/// 内部按 1K char 分块（Segment），前缀检查（如 StartsWithGt 的 ≤4 字符匹配）
/// 几乎总是落在第一个 segment 内，Slice 直接返回内部内存的 span，零拷贝。
/// </summary>
public sealed class LineBuffer
{
    private const int SegmentSize = 1024;

    internal readonly List<char[]> _segments = new();
    private int _length;
    internal int _lastSegFill; // 最后一个 segment 中已填充的字符数

    // 跨 segment 切片时的临时缓冲区（从 ArrayPool 租借）
    private char[]? _scratch;

    /// <summary>缓冲区总字符数。</summary>
    public int Length => _length;

    /// <summary>追加字符块。</summary>
    public void Append(ReadOnlySpan<char> chunk)
    {
        if (chunk.IsEmpty) return;

        int offset = 0;
        while (offset < chunk.Length)
        {
            if (_segments.Count == 0 || _lastSegFill == SegmentSize)
            {
                _segments.Add(new char[SegmentSize]);
                _lastSegFill = 0;
            }

            var seg = _segments[^1];
            int space = SegmentSize - _lastSegFill;
            int toCopy = Math.Min(space, chunk.Length - offset);

            chunk.Slice(offset, toCopy).CopyTo(seg.AsSpan(_lastSegFill));
            _lastSegFill += toCopy;
            _length += toCopy;
            offset += toCopy;
        }
    }

    /// <summary>
    /// 返回 [start, start+length) 范围内的字符跨度。
    ///
    /// 快速路径：若整个范围落在单个 segment 内，直接返回内部内存的 span（零分配）。
    /// 慢速路径：跨 segment 时复制到可复用的临时缓冲区（罕见，仅当前缀 >1K 时触发）。
    /// </summary>
    public ReadOnlySpan<char> Slice(int start, int length)
    {
        if (start < 0 || length < 0 || start + length > _length)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length == 0)
            return [];

        // 定位起始 segment
        int accum = 0;
        int segIdx = 0;
        int curSegLen;
        for (; segIdx < _segments.Count; segIdx++)
        {
            curSegLen = segIdx < _segments.Count - 1 ? SegmentSize : _lastSegFill;
            if (accum + curSegLen > start) break;
            accum += curSegLen;
        }
        if (segIdx >= _segments.Count)
            throw new ArgumentOutOfRangeException(nameof(start));

        int segOffset = start - accum;
        curSegLen = segIdx < _segments.Count - 1 ? SegmentSize : _lastSegFill;

        // 快速路径：整个范围落在单个 segment 内
        if (segOffset + length <= curSegLen)
            return _segments[segIdx].AsSpan(segOffset, length);

        // 慢速路径：跨 segment 复制到临时缓冲区
        return CopyCrossSegment(start, length, segIdx, segOffset);
    }

    /// <summary>清空缓冲区。</summary>
    public void Clear()
    {
        _segments.Clear();
        _length = 0;
        _lastSegFill = 0;
    }

    /// <summary>截断到指定长度。length 必须 ≤ 当前 Length。</summary>
    public void Truncate(int length)
    {
        if (length < 0 || length > _length)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (length == _length) return;

        _length = length;

        int accum = 0;
        for (int i = 0; i < _segments.Count; i++)
        {
            int segLen = SegmentSize;
            if (accum + segLen > length)
            {
                _lastSegFill = length - accum;
                _segments.RemoveRange(i + 1, _segments.Count - i - 1);
                return;
            }
            accum += segLen;
        }

        _lastSegFill = length - accum;
    }

    private ReadOnlySpan<char> CopyCrossSegment(int start, int length, int startSegIdx, int startSegOffset)
    {
        if (_scratch is null || _scratch.Length < length)
        {
            if (_scratch is not null)
                System.Buffers.ArrayPool<char>.Shared.Return(_scratch);
            _scratch = System.Buffers.ArrayPool<char>.Shared.Rent(length);
        }

        int remaining = length;
        int dst = 0;
        for (int i = startSegIdx; i < _segments.Count && remaining > 0; i++)
        {
            int segLen = i < _segments.Count - 1 ? SegmentSize : _lastSegFill;
            int avail = segLen - startSegOffset;
            int toCopy = Math.Min(avail, remaining);
            _segments[i].AsSpan(startSegOffset, toCopy).CopyTo(_scratch.AsSpan(dst));
            dst += toCopy;
            remaining -= toCopy;
            startSegOffset = 0; // 后续 segment 从头开始
        }

        return _scratch.AsSpan(0, length);
    }
}

/// <summary>
/// LineBuffer 的只读视图，通过段枚举避免跨段复制。
/// 供解析器写入 Append 方法使用，实际写入仅由 StreamParser.Feed 完成。
/// </summary>
public readonly struct LineBufferView
{
    private readonly LineBuffer _buffer;

    internal LineBufferView(LineBuffer buffer) => _buffer = buffer;

    /// <summary>缓冲区总字符数。</summary>
    public int Length => _buffer is null ? 0 : _buffer.Length;

    /// <summary>返回 [start, start+length) 范围内的字符跨度（可能触发跨段复制）。</summary>
    public ReadOnlySpan<char> Slice(int start, int length) => _buffer.Slice(start, length);

    /// <summary>返回当前缓冲区的完整内容 span（可能触发跨段复制，仅用于过渡）。</summary>
    public ReadOnlySpan<char> AsSpan() => _buffer.Slice(0, _buffer.Length);

    /// <summary>零分配逐段枚举。</summary>
    public LineBufferSegmentEnumerator GetEnumerator()
        => new LineBufferSegmentEnumerator(_buffer);

    /// <summary>检查当前行是否为空白。</summary>
    public bool IsBlankLine()
    {
        foreach (var seg in this)
            if (!seg.IsWhiteSpace())
                return false;
        return true;
    }
}

/// <summary>
/// 零分配的段枚举器，foreach 使用，不触发跨段复制。
/// </summary>
public ref struct LineBufferSegmentEnumerator
{
    private readonly LineBuffer _buffer;
    private int _index;

    internal LineBufferSegmentEnumerator(LineBuffer? buffer)
    {
        _buffer = buffer!;
        _index = -1;
    }

    public readonly ReadOnlySpan<char> Current
    {
        get
        {
            var seg = _buffer._segments[_index];
            int len = _index < _buffer._segments.Count - 1 ? 1024 : _buffer._lastSegFill;
            return seg.AsSpan(0, len);
        }
    }

    public bool MoveNext() => _buffer is not null && ++_index < _buffer._segments.Count;
}
