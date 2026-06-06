using System.Collections.ObjectModel;

namespace MarkDrip.Model;

/// <summary>
/// 表示列表容器（无序或有序）。
/// 包含 <see cref="ListItemBlock"/> 子项的集合。
/// </summary>
public sealed class ListBlock : DocumentBlock
{
    /// <summary>
    /// 获取列表样式（无序或有序）。
    /// </summary>
    public ListStyle Style { get; }

    /// <summary>
    /// 标记的缩进量（前导空格数，0-3）。
    /// </summary>
    public int MarkerIndent { get; internal set; }

    /// <summary>
    /// 标记的总宽度（缩进 + 标记符 + 后缀空格）。
    /// </summary>
    public int MarkerLength { get; internal set; }

    /// <summary>
    /// 标记符。无序列表为 '-' / '*' / '+'，有序列表为 '.' / ')'。
    /// </summary>
    public char MarkerChar { get; internal set; }

    /// <summary>
    /// 获取此列表中的列表项。
    /// </summary>
    public ObservableCollection<ListItemBlock> Items { get; } = new();

    /// <summary>
    /// 获取或设置是否为"宽松"列表（项之间有空行分隔，使用段落间距渲染）。
    /// </summary>
    public bool IsLoose
    {
        get => _isLoose;
        internal set
        {
            if (_isLoose == value) return;
            _isLoose = value;
            OnPropertyChanged();
        }
    }
    private bool _isLoose;

    /// <summary>
    /// 初始化 <see cref="ListBlock"/> 的新实例。
    /// </summary>
    /// <param name="style">列表样式。</param>
    public ListBlock(ListStyle style)
    {
        Kind = BlockKind.List;
        Style = style;
    }
}
