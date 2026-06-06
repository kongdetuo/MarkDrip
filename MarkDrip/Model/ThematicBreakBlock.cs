namespace MarkDrip.Model;

/// <summary>
/// 表示主题分隔线（水平线）。
/// 创建后立即定稿。
/// </summary>
public sealed class ThematicBreakBlock : DocumentBlock
{
    /// <summary>
    /// 初始化 <see cref="ThematicBreakBlock"/> 的新实例。
    /// 块立即标记为 <see cref="BlockStatus.Finalized"/>。
    /// </summary>
    public ThematicBreakBlock()
    {
        Kind = BlockKind.ThematicBreak;
        Status = BlockStatus.Finalized;
    }
}
