using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

interface IBlockParser
{
    MatchResult TryMatch(ReadOnlySpan<char> line, ParserContext context);
    /// <summary>TryMatch 返回 FullMatch 后调用。解析器在此初始化状态，内容写入统一走 Append。</summary>
    void OnMatch(ReadOnlySpan<char> line, ParserContext context);
    /// <summary>追加数据。返回 KeepFeeding 表示还可继续接收当前块，NeedMatch 表示当前行处理完毕需重新匹配。</summary>
    AppendResult Append(TextChuck chunk, ParserContext context);
    /// <summary>通知解析器输入结束。用于定稿仍处于 Open 的块、Seal InlineCollection。</summary>
    void Complete(ParserContext context) { }
}

public readonly ref struct TextChuck
{
    public readonly ReadOnlySpan<char> Text;
    public readonly bool IsLineStart;
    public readonly bool IsLineEnd;
    public TextChuck(ReadOnlySpan<char> text, bool isLineStart, bool isLineEnd)
    {
        Text = text;
        IsLineStart = isLineStart;
        IsLineEnd = isLineEnd;
    }
}

enum AppendResult
{
    /// <summary>解析器还可继续接收内容，保留 currentParser 不变。</summary>
    KeepFeeding,
    /// <summary>当前行处理完毕，让下行重新经过 TryMatch。</summary>
    NeedMatch,
    /// <summary>当前行未被消费，释放 currentParser 后重新经过 TryMatch（等同行被"退还"给外层）。</summary>
    YieldLine,
}

enum MatchResult
{
    NoMatch,
    PartialMatch,
    FullMatch
}
