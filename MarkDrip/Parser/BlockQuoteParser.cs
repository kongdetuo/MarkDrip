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
        // 最多 3 前导空格 + >（4 空格优先交给缩进代码块）
        if (TextUtils.LeadingSpaces(chunk.Text) <= 3 && StartsWithGt(chunk.Text, out _))
            return MatchResult.FullMatch;

        // 前导空格，非行尾 → 可能累积成 > 前缀
        if (chunk.IsLineStart && chunk.IsBlank && !chunk.IsLineEnd)
            return MatchResult.PartialMatch;

        return MatchResult.NoMatch;
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

        // 非行首 → 延续行，直接追加（无前缀剥离）
        if (!chunk.IsLineStart)
        {
            _innerParser.Feed(chunk.Text);
            return AppendResult.KeepFeeding;
        }

        // 行首未结束的空白 → 等待更多输入
        if (chunk.IsBlank && !chunk.IsLineEnd)
            return AppendResult.NeedNextChunk;

        var line = chunk.Text;

        // ── 以下处理完整行（IsLineEnd = true，空白未结束已在上面拦截） ──

        // 有 > 前缀 → 剥离后转发
        if (StartsWithGt(line, out int prefixLen))
        {
            var content = line[prefixLen..];
            if (content.Length == 0)
                return AppendResult.NeedNextChunk;
            _innerParser.Feed(content);
            return AppendResult.KeepFeeding;
        }

        // 空白行 → 结束引用
        if (line.IsWhiteSpace())
        {
            _innerParser.Feed(line);
            _innerParser.Complete();
            return AppendResult.NextLineNeedMatch;
        }

        // 惰性延续（无 > 的非空白完整行）
        _innerParser.Feed(line);
        return AppendResult.KeepFeeding;
    }

    public void Complete(ParserContext context)
    {
        _innerParser?.Complete();
    }
}
