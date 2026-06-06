namespace MarkDrip.Model;

/// <summary>
/// 定义 Markdown 文档中块的类型。
/// </summary>
public enum BlockKind
{
    /// <summary>
    /// 文本段落。
    /// </summary>
    Paragraph,

    /// <summary>
    /// ATX 或 Setext 标题（级别 1-6）。
    /// </summary>
    Heading,

    /// <summary>
    /// 围栏式或缩进式代码块。
    /// </summary>
    CodeBlock,

    /// <summary>
    /// 主题分隔线（水平线）。
    /// </summary>
    ThematicBreak,

    /// <summary>
    /// 列表容器（无序或有序）。
    /// </summary>
    List,

    /// <summary>
    /// 列表中的单个项。
    /// </summary>
    ListItem,

    /// <summary>
    /// 块引用容器。
    /// </summary>
    BlockQuote,

    /// <summary>
    /// 表格。
    /// </summary>
    Table,
}
