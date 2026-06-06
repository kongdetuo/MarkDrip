namespace MarkDrip.Model;

/// <summary>
/// 表示 ATX 或 Setext 标题（级别 1-6）。
/// </summary>
public sealed class HeadingBlock : DocumentBlock
{
    private readonly int _level;

    /// <summary>
    /// 获取标题级别（1 = 最大，6 = 最小）。
    /// </summary>
    public int Level
    {
        get => _level;
        private init
        {
            if (value is < 1 or > 6)
                throw new ArgumentOutOfRangeException(nameof(value), "Heading level must be between 1 and 6.");
            _level = value;
        }
    }

    /// <summary>
    /// 获取此标题的内联内容。
    /// </summary>
    public InlineCollection Inlines { get; } = new();

    /// <summary>
    /// 初始化 <see cref="HeadingBlock"/> 的新实例。
    /// </summary>
    /// <param name="level">标题级别（1-6）。</param>
    public HeadingBlock(int level)
    {
        if (level is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(level), "Heading level must be between 1 and 6.");
        _level = level;
        Kind = BlockKind.Heading;
        Inlines.CollectionChanged += (_, _) => NotifyContentChanged();
    }
}
