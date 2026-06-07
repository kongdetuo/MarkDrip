using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class ThematicBreakParser : IBlockParser
{
    public MatchResult TryMatch(TextChunk chunk, ParserContext context)
    {
        var line = chunk.Text;
        int pos = TextUtils.LeadingSpaces(line);
        if (pos >= line.Length) return MatchResult.NoMatch;

        char marker = line[pos];
        if (marker is not '-' and not '*' and not '_') return MatchResult.NoMatch;

        if (marker is '-' && context.PreviousBlock is ParagraphBlock { Status: BlockStatus.Open })
            return MatchResult.NoMatch;

        int count = 0;
        while (pos < line.Length)
        {
            if (line[pos] == marker) { count++; pos++; }
            else if (line[pos] is ' ' or '\t') { pos++; }
            else break;
        }

        bool hasNewline = false;
        while (pos < line.Length)
        {
            char c = line[pos];
            if (c is '\n' or '\r') { hasNewline = true; pos++; }
            else if (c is ' ' or '\t') { pos++; }
            else return MatchResult.NoMatch;
        }

        if (count >= 3 && hasNewline)
            return MatchResult.FullMatch;

        if (count > 0 && !hasNewline)
            return MatchResult.PartialMatch;

        return MatchResult.NoMatch;
    }

    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        context.Blocks.Add(new ThematicBreakBlock());
    }

    public AppendResult Append(TextChunk chunk, ParserContext context)
    {
        return AppendResult.NextLineNeedMatch;
    }
}
