using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class ParagraphParser : IBlockParser
{
    private bool _needsLineBreak;

    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        // OnMatch 不重置换行状态：上一个 chunk 可能以 \n 结尾，
        // _needsLineBreak 需要保留到 Append 中判断。
    }

    public AppendResult Append(TextChuck chunk, ParserContext context)
    {
        // 纯空白行（含换行符）
        if (chunk.Text.IsWhiteSpace() && (chunk.Text.Contains('\n') || chunk.Text.Contains('\r')))
        {
            if (context.PreviousBlock is ParagraphBlock { Status: BlockStatus.Open } openPara)
            {
                if (_needsLineBreak)
                {
                    // 已在行边界上 → 第二行空白是真正的空行 → 结束段落
                    openPara.Status = BlockStatus.Finalized;
                    return AppendResult.NeedMatch;
                }
                // 行终止符（在段落内产生软换行）
                _needsLineBreak = true;
                return AppendResult.NeedMatch;
            }
            if (context.PreviousBlock is ParagraphBlock prevPara)
                prevPara.Status = BlockStatus.Finalized;
            _needsLineBreak = true;
            return AppendResult.NeedMatch;
        }

        var para = context.PreviousBlock as ParagraphBlock;
        if (para is not { Status: BlockStatus.Open })
        {
            para = new ParagraphBlock();
            context.Blocks.Add(para);
            _needsLineBreak = false;
        }
        else if (_needsLineBreak)
        {
            // 上一个 chunk 以 \n 结尾 → 这是新的一行，插入软换行
            para.Inlines.AppendLine();
            _needsLineBreak = false;
        }

        var terminatorIdx = chunk.Text.IndexOfAny(new[] { '\n', '\r' });
        if (terminatorIdx >= 0)
        {
            para.Inlines.Append(chunk.Text[..terminatorIdx].ToString());
            _needsLineBreak = true;
            return AppendResult.NeedMatch;
        }

        para.Inlines.Append(chunk.Text.ToString());
        return AppendResult.KeepFeeding;
    }

    public MatchResult TryMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        return MatchResult.NoMatch;
    }

    public void Complete(ParserContext context)
    {
        // 定稿最后一个未完结的段落
        if (context.PreviousBlock is ParagraphBlock { Status: BlockStatus.Open } para)
        {
            para.Status = BlockStatus.Finalized;
            para.Inlines.Seal();
        }
    }
}
