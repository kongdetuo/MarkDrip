using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarkDrip.Model;

/// <summary>
/// 所有 Markdown 文档块的抽象基类。
/// 提供标识（Id）、类型（Kind）、生命周期（Status）和变更通知。
/// </summary>
public abstract class DocumentBlock : INotifyPropertyChanged
{
    private BlockStatus _status = BlockStatus.Open;

    /// <summary>
    /// 获取块类型。
    /// </summary>
    public BlockKind Kind { get; protected set; }

    /// <summary>
    /// 获取或设置此块的生命周期状态。
    /// 只有解析器应将其设置为 <see cref="BlockStatus.Finalized"/>。
    /// </summary>
    public BlockStatus Status
    {
        get => _status;
        internal set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 获取此块的稳定唯一标识符，用于 UI 差异比较。
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// 当属性值发生更改时触发。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 触发 <see cref="PropertyChanged"/> 事件。
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 通知侦听器此块的内容已更改（例如追加了文本）。
    /// 渲染器应重新读取块的内容并更新对应的 UI 控件。
    /// </summary>
    internal void NotifyContentChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Content"));
    }
}
