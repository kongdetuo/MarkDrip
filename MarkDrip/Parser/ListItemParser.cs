using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

/// <summary>
/// 管理单个列表项的内容流式转发。
/// 持有 StreamParser 将已去除缩进的内容喂给内层，处理流式缩进缓冲、首行/续行状态。
/// ListParser 路由到 ListItemParser 后，不再干预字符级流式细节。
/// </summary>
class ListItemParser
{
    private StreamParser? _innerParser;
    private int _contentIndent;

    private bool _consumingFirstLine;
    private int _pendingWs;
    private bool _consumingContinuationLine;
    private int _blankLineCount;

    public bool IsConsuming => _consumingFirstLine || _consumingContinuationLine;
    public StreamParser? InnerParser => _innerParser;
    public int BlankLineCount => _blankLineCount;

    public void StartNewItem(int contentIndent, StreamParser innerParser)
    {
        _innerParser?.Complete();
        _innerParser = innerParser;
        _contentIndent = contentIndent;
        _consumingFirstLine = true;
        _pendingWs = 0;
        _consumingContinuationLine = false;
        _blankLineCount = 0;
    }

    public void FeedFirstLineContent(ReadOnlySpan<char> line)
    {
        if (line.Length > _contentIndent)
            _innerParser!.Feed(line[_contentIndent..]);
        _consumingFirstLine = !line.Contains('\n');
        _pendingWs = 0;
        _consumingContinuationLine = false;
    }

    /// <summary>
    /// 全权处理当前 chunk 的内容路由。
    /// 三部曲依次尝试：流式转发 → 缩进积累/启动续行 → LB 全行裁决。
    /// 返回 AppendResult 可直接由 ListParser.Append 返回。
    /// </summary>
    public AppendResult Forward(ReadOnlySpan<char> chunk, ParserContext context)
    {
        // ── 1. 流式转发：正在转发上一个首行/续行的剩余内容 ──
        if (TryForwardStreaming(chunk))
            return AppendResult.KeepFeeding;

        // ── 2a. 无换行的片段：尝试空白积累或启动续行 ──
        if (!chunk.Contains('\n') && !chunk.Contains('\r'))
        {
            if (TryAccumulateWhitespace(chunk))         // 纯空白 → 累积 or 启动续行
                return AppendResult.KeepFeeding;
            if (TryStartContinuation(chunk))            // 有内容 → 缩进跨阈即续行
                return AppendResult.KeepFeeding;
        }
        _pendingWs = 0;                                 // 不满足任何条件，清空积累

        // ── 2b. 全行裁决：等 LB 中有完整行后判定归属 ──
        return RouteFullLine(context);
    }

    /// <summary>
    /// 状态      | 含义                     | 当前 chunk 怎么处理
    /// ---------|--------------------------|--------------------
    /// _consumingFirstLine       | 正在转发第一个 item 的首行内容 | 直接喂 innerParser
    /// _consumingContinuationLine | 正在转发续行的内容              | 直接喂 innerParser
    /// 两者都假  | 未处于流式转发状态           | 尝试缩进积累或 LB 裁决
    ///
    /// 返回 true 表示 chunk 已被消费为流式内容。
    /// </summary>
    private bool TryForwardStreaming(ReadOnlySpan<char> chunk)
    {
        if (_consumingFirstLine)
        {
            _innerParser!.Feed(chunk);
            if (chunk.Contains('\n') || chunk.Contains('\r'))
                _consumingFirstLine = false;
            return true;
        }

        if (_consumingContinuationLine)
        {
            _innerParser!.Feed(chunk);
            if (chunk.Contains('\n') || chunk.Contains('\r'))
                _consumingContinuationLine = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// chunk 全是空白且无换行：
    /// - 若 _pendingWs 还未到达 _contentIndent，则累加
    /// - 若已越过阈值，空白属于续行内容，直接启动续行转发
    /// </summary>
    private bool TryAccumulateWhitespace(ReadOnlySpan<char> chunk)
    {
        if (!chunk.IsWhiteSpace())
            return false;

        if (_pendingWs < _contentIndent)
            _pendingWs += chunk.Length;
        else
        {
            _consumingContinuationLine = true;
            _pendingWs = 0;
            _innerParser!.Feed(chunk);
        }
        return true;
    }

    /// <summary>
    /// chunk 非纯空白且无换行：统计前导空格能否跨过 _contentIndent 阈值。
    /// 若能 → 启动续行转发（chunk 中超出缩进的部分喂 innerParser）。
    /// 若不能 → 此 chunk 不是续行，返回 false 让调用方裁决。
    /// </summary>
    private bool TryStartContinuation(ReadOnlySpan<char> chunk)
    {
        int leading = 0;
        while (leading < chunk.Length && _pendingWs + leading < _contentIndent && chunk[leading] == ' ')
            leading++;
        if (_pendingWs + leading >= _contentIndent)
        {
            _consumingContinuationLine = true;
            _pendingWs = 0;
            _innerParser!.Feed(chunk[leading..]);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 利用 LastLineBuffer 中的完整行做最终裁决：
    ///   空白行 → _blankLineCount++
    ///   续  行 → 清除前缀空白后喂 innerParser（触发前先 flush 积压的空白行）
    ///   均不是 → YieldLine（当前内容不属于此列表项）
    /// </summary>
    private AppendResult RouteFullLine(ParserContext context)
    {
        var lb = context.LastLineBuffer;
        var fullLine = lb.AsSpan();

        if (lb.IsBlankLine())
        {
            _blankLineCount++;
            return AppendResult.KeepFeeding;
        }

        if (IsContinuation(fullLine))
        {
            if (_blankLineCount > 0) { FlushBlankLines(); _blankLineCount = 0; }
            FeedContinuationLine(fullLine);
            return AppendResult.KeepFeeding;
        }

        return AppendResult.YieldLine;
    }

    public void FeedContinuationLine(ReadOnlySpan<char> fullLine)
    {
        _pendingWs = 0;
        if (_contentIndent < fullLine.Length)
            _innerParser!.Feed(fullLine[_contentIndent..]);
    }

    /// <summary>
    /// Complete 时处理 LB 中残留内容，然后定稿 innerParser。
    /// </summary>
    public void CompleteForward(ParserContext context)
    {
        if (!IsConsuming)
        {
            var lb = context.LastLineBuffer;
            var rest = lb.AsSpan();
            if (rest.Length > 0)
            {
                if (IsContinuation(rest))
                    FeedContinuationLine(rest);
                else if (_innerParser != null)
                    _innerParser.Feed(rest.ToString());
            }
        }
        Complete();
    }

    public void Reset()
    {
        _innerParser?.Complete();
        _innerParser = null;
        _consumingFirstLine = false;
        _pendingWs = 0;
        _consumingContinuationLine = false;
        _blankLineCount = 0;
    }

    public void Complete()
    {
        _innerParser?.Complete();
    }

    private bool IsContinuation(ReadOnlySpan<char> line)
    {
        int ws = 0;
        while (ws < line.Length && ws < _contentIndent && line[ws] == ' ')
            ws++;
        return ws >= _contentIndent;
    }

    private void FlushBlankLines()
    {
        for (int i = 0; i < _blankLineCount; i++)
            _innerParser!.Feed("\n");
        _blankLineCount = 0;
    }

}
