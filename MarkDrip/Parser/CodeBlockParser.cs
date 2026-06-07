using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class CodeBlockParser : IBlockParser
{
    public MatchResult TryMatch(TextChunk chunk, ParserContext context)
    {
        var line = chunk.Text;
        int pos = TextUtils.LeadingSpaces(line);
        if (pos >= line.Length) return MatchResult.NoMatch;
        char c = line[pos];
        if (c is not '`' and not '~') return MatchResult.NoMatch;

        int count = 0;
        while (pos < line.Length && line[pos] == c) { count++; pos++; }
        if (count >= 3) return MatchResult.FullMatch;
        return pos >= line.Length ? MatchResult.PartialMatch : MatchResult.NoMatch;
    }

    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        context.Blocks.Add(new CodeBlock { InfoStringPending = true });
    }

    public AppendResult Append(TextChunk chunk, ParserContext context)
    {
        if (context.PreviousBlock is not CodeBlock code)
            return AppendResult.NextLineNeedMatch;

        // ── 首次 Append：解析开围栏，不论是否完结都不转发此行 ──
        if (code.InfoStringPending)
        {
            if (ParseOpenFence(code, chunk.Text, context))
                code.InfoStringPending = false;
            return AppendResult.KeepFeeding;
        }

        // ── 正常转发内容 ──
        code.AppendContent(chunk.Text);

        if (chunk.Text.Contains('\n'))
        {
            var lb = context.LastLineBuffer;
            int end = lb.Length;
            if (end > 0 && lb.Slice(end - 1, 1)[0] == '\n') end--;
            if (end > 0 && lb.Slice(end - 1, 1)[0] == '\r') end--;

            if (IsClosingFence(lb.Slice(0, end), code.OpenFenceChar, code.OpenFenceLength))
            {
                code.Content.Remove(code.Content.Length - lb.Length, lb.Length);
                StripTrailingNewline(code);
                code.Status = BlockStatus.Finalized;
                return AppendResult.NextLineNeedMatch;
            }
        }

        return AppendResult.KeepFeeding;
    }

    private static bool ParseOpenFence(CodeBlock code, ReadOnlySpan<char> chunk, ParserContext context)
    {
        if (code.OpenFenceChar == '\0')
        {
            int pos = TextUtils.LeadingSpaces(chunk);
            if (pos >= chunk.Length) return false;
            code.OpenFenceChar = chunk[pos];
            int count = 0;
            while (pos < chunk.Length && chunk[pos] == code.OpenFenceChar) { count++; pos++; }
            code.OpenFenceLength = count;
        }

        if (!chunk.Contains('\n')) return false;

        var lb = context.LastLineBuffer;
        var line = lb.AsSpan();
        int end = line.Length;
        if (end > 0 && line[end - 1] == '\n') end--;
        if (end > 0 && line[end - 1] == '\r') end--;

        if (end > code.OpenFenceLength)
        {
            var info = line.Slice(code.OpenFenceLength, end - code.OpenFenceLength).TrimEnd();
            var trimmed = info.TrimStart();
            if (trimmed.Length > 0)
            {
                var infoStr = trimmed.ToString();
                if (code.OpenFenceChar != '`' || !infoStr.Contains('`'))
                    code.InfoString = infoStr;
            }
        }
        return true;
    }

    private static bool IsClosingFence(ReadOnlySpan<char> line, char fenceChar, int fenceLength)
    {
        int pos = TextUtils.LeadingSpaces(line);
        if (pos >= line.Length || line[pos] != fenceChar)
            return false;

        int count = 0;
        while (pos < line.Length && line[pos] == fenceChar) { count++; pos++; }

        if (count < fenceLength) return false;
        return fenceChar != '`' || TextUtils.IsWhitespaceOnly(line[pos..]);
    }

    public void Complete(ParserContext context)
    {
        if (context.PreviousBlock is not CodeBlock code) return;
        if (code.InfoStringPending)
        {
            var lb = context.LastLineBuffer;
            if (lb.Length > code.OpenFenceLength)
            {
                var info = lb.Slice(code.OpenFenceLength, lb.Length - code.OpenFenceLength).Trim();
                if (info.Length > 0)
                {
                    if (code.OpenFenceChar != '`' || !info.ToString().Contains('`'))
                        code.InfoString = info.ToString();
                }
            }
            code.InfoStringPending = false;
        }
        StripTrailingNewline(code);
    }

    private static void StripTrailingNewline(CodeBlock code)
    {
        while (code.Content.Length > 0)
        {
            var last = code.Content[^1];
            if (last != '\n' && last != '\r') break;
            code.Content.Remove(code.Content.Length - 1, 1);
            code.NotifyContentChanged();
        }
    }

}
