using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

interface IBlockParser
{
    MatchResult TryMatch(TextChunk chunk, ParserContext context);
    /// <summary>TryMatch 返回 FullMatch 后调用。解析器在此初始化状态，内容写入统一走 Append。</summary>
    void OnMatch(ReadOnlySpan<char> line, ParserContext context);
    /// <summary>追加数据。返回 KeepFeeding 表示还可继续接收当前块，NeedMatch 表示当前行处理完毕需重新匹配。</summary>
    AppendResult Append(TextChunk chunk, ParserContext context);
    /// <summary>通知解析器输入结束。用于定稿仍处于 Open 的块、Seal InlineCollection。</summary>
    void Complete(ParserContext context) { }
}

public readonly ref struct TextChunk
{
    public readonly ReadOnlySpan<char> Text;
    public readonly bool IsLineStart;
    public readonly bool IsLineEnd;
    public TextChunk(ReadOnlySpan<char> text, bool isLineStart, bool isLineEnd)
    {
        Text = text;
        IsLineStart = isLineStart;
        IsLineEnd = isLineEnd;
    }

    public bool IsBlank => TextUtils.IsWhitespaceOnly(Text);
    public int Length => Text.Length;
}

enum AppendResult
{
    /// <summary>解析器还可继续接收内容，保留 currentParser 不变。</summary>
    KeepFeeding,
    /// <summary>当前行处理完毕，让下行重新经过 TryMatch。</summary>
    NextLineNeedMatch,
    /// <summary>当前行未被消费，释放 currentParser 后重新经过 TryMatch（等同行被"退还"给外层）。</summary>
    ReMatch,
    /// <summary>
    /// 需要更多内容才能确定是否消费当前块
    /// </summary>
    NeedNextChunk,
}

enum MatchResult
{
    NoMatch,
    PartialMatch,
    FullMatch
}
