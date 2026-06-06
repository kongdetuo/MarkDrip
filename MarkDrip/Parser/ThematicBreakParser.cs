using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class ThematicBreakParser : IBlockParser
{
    public MatchResult TryMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        int pos = TextUtils.LeadingSpaces(line);
        if (pos >= line.Length) return MatchResult.NoMatch;

        char marker = line[pos];
        if (marker is not '-' and not '*' and not '_') return MatchResult.NoMatch;

        // Setext 标题只使用 '-'（和 '='），当上一段未结束时 '-' 标记优先给 SetextHeaderParser 处理
        if (marker is '-' && context.PreviousBlock is ParagraphBlock { Status: BlockStatus.Open })
            return MatchResult.NoMatch;

        // 扫描标记和中间空格/制表符
        int count = 0;
        while (pos < line.Length)
        {
            if (line[pos] == marker) { count++; pos++; }
            else if (line[pos] is ' ' or '\t') { pos++; }
            else break;
        }

        // 尾部只能有空白/换行
        bool hasNewline = false;
        while (pos < line.Length)
        {
            char c = line[pos];
            if (c is '\n' or '\r') { hasNewline = true; pos++; }
            else if (c is ' ' or '\t') { pos++; }
            else return MatchResult.NoMatch;
        }

        // 条件 2+3：完整行（含换行符）+ 至少 3 个相同标记
        if (count >= 3 && hasNewline)
            return MatchResult.FullMatch;

        // 有标记但行不完整（无换行符），等待后续字符累积后再尝试
        if (count > 0 && !hasNewline)
            return MatchResult.PartialMatch;

        return MatchResult.NoMatch;
    }

    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        context.Blocks.Add(new ThematicBreakBlock());
    }

    public AppendResult Append(ReadOnlySpan<char> chunk, ParserContext context)
    {
        return AppendResult.NeedMatch;
    }
}
