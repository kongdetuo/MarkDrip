using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class BlockQuoteParser : IBlockParser
{
    private StreamParser? _innerParser;

    /// <summary>判断行是否以块引用前缀开头（最多 3 前导空格 + > + 可选空格）。</summary>
    private static bool StartsWithGt(ReadOnlySpan<char> line, out int prefixLen)
    {
        int pos = TextUtils.LeadingSpaces(line);
        if (pos < line.Length && line[pos] == '>')
        {
            pos++; // skip >
            if (pos < line.Length && line[pos] == ' ')
                pos++;
            prefixLen = pos;
            return true;
        }
        prefixLen = 0;
        return false;
    }

    public MatchResult TryMatch(TextChunk chunk, ParserContext context)
    {
        return StartsWithGt(chunk.Text, out _) ? MatchResult.FullMatch : MatchResult.NoMatch;
    }

    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        // 连续引用行 → 复用已有引用块
        if (context.PreviousBlock is BlockQuoteBlock { Status: BlockStatus.Open })
            return;

        var quote = new BlockQuoteBlock();
        context.Blocks.Add(quote);
        _innerParser = new StreamParser(quote.Children);
    }

    public AppendResult Append(TextChunk chunk, ParserContext context)
    {
        if (_innerParser == null)
            return AppendResult.NextLineNeedMatch;

        var lb = context.LastLineBuffer;
        // LastLineBuffer 已由 StreamParser.Feed 统一追加，此处直接读取

        // ── 检查行首是否有 > 前缀（最多看 5 字符：3 前导空格 + > + 可选空格）──
        int headLen = Math.Min(5, lb.Length);
        var head = headLen > 0 ? lb.Slice(0, headLen) : [];
        if (!StartsWithGt(head, out int prefixLen))
        {
            // 无 > 前缀：空白行 → 结束引用；内容 → 惰性延续
            if (chunk.Text.IsWhiteSpace() && (chunk.Text.Contains('\n') || chunk.Text.Contains('\r')))
            {
                _innerParser.Feed(chunk.Text);
                return AppendResult.NextLineNeedMatch;
            }
            _innerParser.Feed(chunk.Text);
            return AppendResult.KeepFeeding;
        }

        // ── 有 > 前缀 → 剥离前缀，增量喂送 ──
        // LLB 中 prefixLen 之前的字符是前缀。chunk 已被 StreamParser 追加到 LLB 末尾，
        // chunkStart = lb.Length - chunk.Length 即 chunk 在整行中的起始位置。
        int chunkStart = lb.Length - chunk.Text.Length;

        if (chunkStart >= prefixLen)
        {
            // chunk 完全在前缀之后 → 全量喂送
            _innerParser.Feed(chunk.Text);
        }
        else if (chunkStart + chunk.Text.Length <= prefixLen)
        {
            // chunk 完全在前缀之内 → 丢弃（> 或 > 后的空格）
        }
        else
        {
            // 部分重叠 → 只喂前缀之后的子串
            int contentOffset = prefixLen - chunkStart;
            _innerParser.Feed(chunk.Text[contentOffset..]);
        }

        // LLB 由 StreamParser.Feed 在行完结时统一清理，解析器不做修改
        return AppendResult.KeepFeeding;
    }

    public void Complete(ParserContext context)
    {
        _innerParser?.Complete();
    }
}
