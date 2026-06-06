namespace MarkDrip.Model;

/// <summary>
/// 表示文档块的生命周期状态。
/// </summary>
public enum BlockStatus
{
    /// <summary>
    /// 块仍在接收流式内容，解析器可继续追加文本或修改块。
    /// </summary>
    Open,

    /// <summary>
    /// 块已完成，解析器不再修改。定稿后从解析器角度看块不可变。
    /// </summary>
    Finalized,
}
