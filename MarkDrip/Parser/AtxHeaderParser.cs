using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class AtxHeaderParser : IBlockParser
{
    /// <summary>TryMatch 命中后解析级别并创建 HeadingBlock，内容由后续 Append 写入。</summary>
    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        // 解析标题级别（# 个数）
        int level = 0;
        while (level < line.Length && level < 6 && line[level] == '#')
            level++;

        var heading = new HeadingBlock(level);
        context.Blocks.Add(heading);
    }

    public AppendResult Append(TextChunk chunk, ParserContext context)
    {
        if (context.PreviousBlock is not HeadingBlock heading)
            return AppendResult.NextLineNeedMatch;

        if (chunk.IsLineStart)
        {
            var text = ExtractHeadingText(chunk.Text);
            if (text.Length > 0)
                heading.Inlines.Append(text);

            if (chunk.IsLineEnd)
            {
                heading.Status = BlockStatus.Finalized;
                return AppendResult.NextLineNeedMatch;
            }

            return AppendResult.KeepFeeding;
        }

        if (chunk.IsLineEnd)
        {
            var content = TextUtils.StripTrailingNewline(chunk.Text);
            if (content.Length > 0)
                heading.Inlines.Append(content);
            heading.Status = BlockStatus.Finalized;
            return AppendResult.NextLineNeedMatch;
        }

        if (chunk.Text.EndsWith("#"))
            return AppendResult.NeedNextChunk;

        heading.Inlines.Append(chunk.Text);
        return AppendResult.KeepFeeding;
    }

    private static string ExtractHeadingText(ReadOnlySpan<char> line)
    {
        // 遵循 CommonMark 规范提取 ATX 标题文本
        int hashCount = 0;
        while (hashCount < line.Length && hashCount < 6 && line[hashCount] == '#')
            hashCount++;

        // 跳过 # 后的首个空格（CommonMark 要求的间隔）
        int start = hashCount;
        if (start < line.Length && line[start] == ' ')
            start++;

        var content = line[start..];

        // 去除尾部空白（包括 \n, \r, 空格, 制表符）
        content = content.TrimEnd();

        // 去除结尾的闭合 #（CommonMark 允许的可选闭包）
        while (content.Length > 0 && content[^1] == '#')
            content = content[..^1].TrimEnd();

        return content.ToString();
    }

    public MatchResult TryMatch(TextChunk chunk, ParserContext context)
    {
        var line = chunk.Text;
        int hashCount = 0;
        while (hashCount < line.Length && hashCount < 6 && line[hashCount] == '#')
            hashCount++;

        if (hashCount == 0)
            return MatchResult.NoMatch;

        if (hashCount == line.Length)
            return MatchResult.PartialMatch;

        if (hashCount <= 6 && char.IsWhiteSpace(line[hashCount]))
            return MatchResult.FullMatch;

        return MatchResult.NoMatch;
    }
}
