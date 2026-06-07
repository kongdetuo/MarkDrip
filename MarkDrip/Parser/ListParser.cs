using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class ListParser : IBlockParser
{
    private readonly ListItemParser _itemParser = new();
    private ListBlock? _lastList;

    public MatchResult TryMatch(TextChunk chunk, ParserContext context)
    {
        if (TryParseMarker(chunk.Text) != null)
            return MatchResult.FullMatch;
        return CouldBeMarker(chunk.Text) ? MatchResult.PartialMatch : MatchResult.NoMatch;
    }

    private static bool CouldBeMarker(ReadOnlySpan<char> line)
    {
        if (line.IsEmpty) return false;
        int pos = TextUtils.LeadingSpaces(line);
        if (pos >= line.Length)
            return true;

        if (line[pos] is '-' or '*' or '+')
            return pos + 1 >= line.Length;

        if (char.IsDigit(line[pos]))
        {
            int start = pos;
            while (pos < line.Length && char.IsDigit(line[pos]))
                pos++;
            if (pos == start) return false;
            if (pos >= line.Length) return true;
            if (line[pos] is '.' or ')')
            {
                pos++;
                return pos >= line.Length;
            }
        }

        return false;
    }

    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        var mi = TryParseMarker(line)!.Value;

        ListBlock list;
        if (context.PreviousBlock is ListBlock { Status: BlockStatus.Open } existing
            && existing == _lastList
            && mi.MarkerChar == (_lastList?.MarkerChar ?? '\0'))
        {
            list = existing;
        }
        else
        {
            list = new ListBlock(mi.IsOrdered ? ListStyle.Ordered : ListStyle.Bullet);
            context.Blocks.Add(list);
        }

        list.MarkerChar = mi.MarkerChar;
        list.MarkerIndent = mi.Indent;
        list.MarkerLength = mi.MarkerLength;
        _lastList = list;
        _itemParser.Reset();
    }

    public AppendResult Append(TextChunk chunk, ParserContext context)
    {
        var list = context.PreviousBlock as ListBlock;

        // ── 1a. 首次 Append：创建首个 item 并灌入首行 ──
        if (_itemParser.InnerParser == null)
        {
            StartNewItem(list!);
            _itemParser.FeedFirstLineContent(chunk.Text);
            return AppendResult.KeepFeeding;
        }

        // ── 2. 同级别新标记 ──
        if (IsSameMarker(chunk.Text))
        {
            int blc = _itemParser.BlankLineCount;
            if (blc >= 2) { _itemParser.Reset(); _lastList = null; return AppendResult.ReMatch; }
            if (blc == 1) list!.IsLoose = true;
            StartNewItem(list!);
            FeedContentAfterMarker(chunk.Text);
            return AppendResult.KeepFeeding;
        }

        // ── 3. 委托给 ItemParser 全权路由 ──
        return _itemParser.Forward(chunk.Text, context);
    }

    public void Complete(ParserContext context)
    {
        _itemParser.CompleteForward(context);
    }

    private void StartNewItem(ListBlock list)
    {
        var item = new ListItemBlock();
        list.Items.Add(item);
        _itemParser.StartNewItem(list.MarkerIndent + list.MarkerLength, new StreamParser(item.Children));
    }

    private void FeedContentAfterMarker(ReadOnlySpan<char> line)
    {
        int indent = _lastList!.MarkerIndent + _lastList.MarkerLength;
        if (line.Length > indent)
            _itemParser.InnerParser!.Feed(line[indent..]);
    }

    private bool IsSameMarker(ReadOnlySpan<char> line)
    {
        var m = TryParseMarker(line);
        if (m == null) return false;
        return m.Value.MarkerChar == _lastList!.MarkerChar
            && m.Value.Indent == _lastList.MarkerIndent;
    }

    // ===== 标记解析 =====

    /// <summary>
    /// 尝试将行首解析为一个列表标记。
    /// </summary>
    /// <param name="indent">前导空格数 (0–3)</param>
    /// <param name="markerLength">标记总宽度（含结尾所有空白），即 indent + marker_char + 可选空格</param>
    private readonly record struct MarkerInfo(int Indent, int MarkerLength, bool IsOrdered, char MarkerChar);

    private static MarkerInfo? TryParseMarker(ReadOnlySpan<char> line)
    {
        if (line.Length == 0) return null;

        int pos = TextUtils.LeadingSpaces(line);
        int indent = pos;
        if (pos >= line.Length) return null;

        var start = ParseMarkerStart(line, ref pos);
        if (start == null) return null;

        // 标记后必须跟空格 / tab / 换行
        if (pos >= line.Length) return null;
        char after = line[pos];
        if (after != ' ' && after != '\t' && after != '\n' && after != '\r')
            return null;

        // 跳过后续空白，标记总宽度 = 当前位置 - 缩进起点
        while (pos < line.Length && line[pos] == ' ')
            pos++;

        return new MarkerInfo(indent, pos - indent, start.Value.IsOrdered, start.Value.Char);
    }

    /// <summary>
    /// 在 pos 处尝试解析标记起始（子弹字符 / 有序前缀）。
    /// 成功时推进 pos 并返回标记信息；失败时 pos 不变，返回 null。
    /// </summary>
    private static MarkerStart? ParseMarkerStart(ReadOnlySpan<char> line, ref int pos)
    {
        char c = line[pos];

        if (c is '-' or '*' or '+')
        {
            pos++;
            return new MarkerStart(c, false);
        }

        if (char.IsDigit(c))
        {
            int start = pos;
            while (pos < line.Length && char.IsDigit(line[pos]))
                pos++;
            if (pos < line.Length && line[pos] is '.' or ')')
            {
                char delim = line[pos];
                pos++;
                return new MarkerStart(delim, true);
            }
            // 不是有效有序前缀 → 回溯
            pos = start;
        }

        return null;
    }

    private readonly record struct MarkerStart(char Char, bool IsOrdered);
}
