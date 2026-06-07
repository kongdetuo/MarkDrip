using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

/// <summary>缩进代码块解析器。</summary>
class IndentedCodeBlockParser : IBlockParser
{
    public MatchResult TryMatch(TextChunk chunk, ParserContext context)
    {
        // 不能打断段落（CommonMark 4.4）
        if (context.PreviousBlock is ParagraphBlock { Status: BlockStatus.Open })
            return MatchResult.NoMatch;

        // 4 个或更少的纯空白字符（非行尾）——可能累积成缩进代码块，期待更多输入
        if (chunk.Length <= 4 && !chunk.IsLineEnd && chunk.IsBlank)
            return MatchResult.PartialMatch;

        // 前 4 字符是空白、后续有非空白内容 → 匹配
        if (chunk.Length > 4 && chunk.Text[0..4].IsWhiteSpace() && chunk.Text[4..].IsNotWhiteSpace())
            return MatchResult.FullMatch;

        return MatchResult.NoMatch;
    }

    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        // 只创建块，内容由 Append 逐行追加
        var code = new CodeBlock();
        context.Blocks.Add(code);
    }

    public AppendResult Append(TextChunk chunk, ParserContext context)
    {
        if (context.PreviousBlock is not CodeBlock code)
            return AppendResult.NextLineNeedMatch;

        var text = chunk.Text;
        if (chunk.IsLineStart)
        {
            // ── 新行判定 ──
            int checkLen = Math.Min(4, chunk.Length);

            // 行首 4 字符非空白 → 代码块到此结束，将当前行归还给 StreamParser 重新匹配
            if (chunk.Text[..checkLen].IsNotWhiteSpace())
            {
                StripTrailingBlankLines(code);
                code.Status = BlockStatus.Finalized;
                return AppendResult.ReMatch;
            }

            // ── 缩进的内容行 ──
            if (chunk.Length > 4)
                text = chunk.Text[4..];
            else
                text = "\n";
        }

        // 追加，然后等待下一个块。
        code.AppendContent(text);
        return AppendResult.KeepFeeding;
    }

    public void Complete(ParserContext context)
    {
        if (context.PreviousBlock is CodeBlock { Status: BlockStatus.Open } code)
        {
            StripTrailingBlankLines(code);
            code.Status = BlockStatus.Finalized;
        }
    }

    /// <summary>移除代码块内容末尾的所有空行（\n 和 \r）。</summary>
    private static void StripTrailingBlankLines(CodeBlock code)
    {
        while (code.Content.Length > 0)
        {
            var last = code.Content[^1];
            if (char.IsWhiteSpace(last))
                code.Content.Remove(code.Content.Length - 1, 1);
            else
                break;
        }
        code.NotifyContentChanged();
    }
}
