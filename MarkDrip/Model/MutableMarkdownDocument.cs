using System.Collections.ObjectModel;
using MarkDrip.Parser;

namespace MarkDrip.Model;

/// <summary>
/// 流式 Markdown 的根文档模型。
/// 内部持有 <see cref="Parser.StreamParser"/>，是 MVVM 交互的核心。
/// 调用方只需 <c>document.Append(text)</c>，新块自动进入 <see cref="Blocks"/>。
/// </summary>
public sealed class MutableMarkdownDocument
{
    private StreamParser? _parser;

    /// <summary>
    /// 获取文档中块的有序集合。
    /// 随着流被解析，新块被追加到末尾。
    /// </summary>
    public ObservableCollection<DocumentBlock> Blocks { get; } = new();

    /// <summary>
    /// 获取文档是否没有任何块。
    /// </summary>
    public bool IsEmpty => Blocks.Count == 0;

    /// <summary>
    /// 获取文档中的最后一个块，如果文档为空则为 null。
    /// </summary>
    public DocumentBlock? LastBlock =>
        Blocks.Count > 0 ? Blocks[^1] : null;

    /// <summary>
    /// 将文本块输入解析器。内部持有 Parser，自动追加到 <see cref="Blocks"/>。
    /// </summary>
    public void Append(ReadOnlySpan<char> text)
    {
        _parser ??= new StreamParser(Blocks);
        _parser.Feed(text);
    }

    /// <summary>
    /// 将文本块输入解析器。
    /// </summary>
    public void Append(string text) => Append(text.AsSpan());

    /// <summary>
    /// 表示输入流已完成。
    /// 刷新所有未完成块，定稿文档。
    /// </summary>
    public void Complete()
    {
        _parser?.Complete();
    }
}
