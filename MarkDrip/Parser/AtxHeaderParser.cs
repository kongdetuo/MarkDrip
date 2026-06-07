using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class AtxHeaderParser : IBlockParser
{
    private bool _initialContentProcessed;

    /// <summary>TryMatch 命中后解析级别并创建 HeadingBlock，内容由后续 Append 写入。</summary>
    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        // 解析标题级别（# 个数）
        int level = 0;
        while (level < line.Length && level < 6 && line[level] == '#')
            level++;

        var heading = new HeadingBlock(level);
        context.Blocks.Add(heading);
        _initialContentProcessed = false;
    }

    public AppendResult Append(TextChuck chunk, ParserContext context)
    {
        if (context.PreviousBlock is not HeadingBlock heading)
            return AppendResult.NeedMatch;

        var hasNewline = chunk.Text.Contains('\n') || chunk.Text.Contains('\r');

        if (!_initialContentProcessed)
        {
            _initialContentProcessed = true;
            // 首次调用：提取标题文本（去除 # 标记和尾部空格 / #）
            var text = ExtractHeadingText(chunk.Text);
            if (text.Length > 0)
                heading.Inlines.Append(text);
        }
        else
        {
            // 续接内容（极少发生，仅当标题文本被拆到多个 chunk 时）
            var content = TextUtils.StripTrailingNewline(chunk.Text);
            if (content.Length > 0)
                heading.Inlines.Append(content.ToString());
        }

        return hasNewline ? AppendResult.NeedMatch : AppendResult.KeepFeeding;
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

    public MatchResult TryMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        int hashCount = 0;
        while (hashCount < line.Length && hashCount < 6 && line[hashCount] == '#')
            hashCount++;

        if (hashCount == 0)
            return MatchResult.NoMatch;

        // 只有 # 没有内容 → 需要更多输入才能判断
        if (hashCount == line.Length)
            return MatchResult.PartialMatch;

        // # 后必须是空白字符才构成合法 ATX 标题
        if (hashCount <= 6 && char.IsWhiteSpace(line[hashCount]))
            return MatchResult.FullMatch;

        return MatchResult.NoMatch;
    }
}
