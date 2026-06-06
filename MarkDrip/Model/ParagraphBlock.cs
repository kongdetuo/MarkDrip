namespace MarkDrip.Model;

/// <summary>
/// 表示带有内联格式的文本段落。
/// </summary>
public sealed class ParagraphBlock : DocumentBlock
{
    /// <summary>
    /// 获取此段落的内联内容。
    /// </summary>
    public InlineCollection Inlines { get; } = new();

    /// <summary>
    /// 初始化 <see cref="ParagraphBlock"/> 的新实例。
    /// </summary>
    public ParagraphBlock()
    {
        Kind = BlockKind.Paragraph;
        Inlines.CollectionChanged += (_, _) => NotifyContentChanged();
    }
}
