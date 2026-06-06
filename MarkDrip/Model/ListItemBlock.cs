using System.Collections.ObjectModel;

namespace MarkDrip.Model;

/// <summary>
/// 表示 <see cref="ListBlock"/> 中的单个项。
/// 包含其自己的子块（段落、嵌套列表等）。
/// </summary>
public sealed class ListItemBlock : DocumentBlock
{
    /// <summary>
    /// 获取此列表项中的子块。
    /// </summary>
    public ObservableCollection<DocumentBlock> Children { get; } = new();

    /// <summary>
    /// 初始化 <see cref="ListItemBlock"/> 的新实例。
    /// </summary>
    public ListItemBlock()
    {
        Kind = BlockKind.ListItem;
    }
}
