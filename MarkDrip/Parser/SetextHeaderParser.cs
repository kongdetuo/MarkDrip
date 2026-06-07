using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class SetextHeaderParser : IBlockParser
{
    public AppendResult Append(TextChuck chunk, ParserContext context)
    {
        return AppendResult.NeedMatch;
    }

    public MatchResult TryMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        // Setext标题必须在未完成的段落块之后
        if (line.Length > 0 && context.PreviousBlock is ParagraphBlock { Status: BlockStatus.Open })
        {
            // 全都是=或者全都是-，并且至少有一个结束符
            while (line.Length > 0 && char.IsWhiteSpace(line[0]))
                line = line[1..];

            // 纯空白行不可能构成 Setext 标题
            if (line.Length == 0)
                return MatchResult.NoMatch;

            // 判断全都是=或者全都是-
            var c = line[0];
            if(c is '=' or '-')
            {
                for (int i = 1; i < line.Length; i++)
                {
                    if (line[i] == '\r')
                        continue;
                    if (line[i] == '\n')
                        return MatchResult.FullMatch;
                    if (line[i] != c)
                        return MatchResult.NoMatch;
                }
                // 全部是 = 或 -，但没有 \n — 需要更多输入才能确认
                return MatchResult.PartialMatch;
            }
        }
        return MatchResult.NoMatch;
    }

    /// <summary>将前一段落块就地转换为 Setext 标题。</summary>
    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        if (context.PreviousBlock is not ParagraphBlock para)
            return;

        // 确定标题级别：= → Level 1, - → Level 2
        int level = GetSetextLevel(line);

        // 创建标题块，转移段落的内联内容
        var heading = new HeadingBlock(level);
        var rawContent = para.Inlines.RawBuffer.ToString();
        if (rawContent.Length > 0)
            heading.Inlines.Append(rawContent);

        // 在 Blocks 中用标题替换段落
        int idx = context.Blocks.IndexOf(para);
        context.Blocks.RemoveAt(idx);
        context.Blocks.Insert(idx, heading);
    }

    /// <summary>从 setext 下划线行中提取标题级别。</summary>
    private static int GetSetextLevel(ReadOnlySpan<char> line)
    {
        int pos = 0;
        while (pos < line.Length && char.IsWhiteSpace(line[pos]))
            pos++;
        return pos < line.Length && line[pos] == '=' ? 1 : 2;
    }
}
