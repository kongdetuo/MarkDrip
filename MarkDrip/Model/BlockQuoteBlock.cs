using System.Collections.ObjectModel;

namespace MarkDrip.Model;

/// <summary>
/// 表示块引用容器。
/// 包含子块（段落、列表、标题等）。
/// </summary>
public sealed class BlockQuoteBlock : DocumentBlock
{
    /// <summary>
    /// 获取此块引用中的子块。
    /// </summary>
    public ObservableCollection<DocumentBlock> Children { get; } = new();

    /// <summary>
    /// 初始化 <see cref="BlockQuoteBlock"/> 的新实例。
    /// </summary>
    public BlockQuoteBlock()
    {
        Kind = BlockKind.BlockQuote;
    }
}
