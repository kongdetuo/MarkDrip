using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using MarkDrip.Parser;

namespace MarkDrip.Model;

/// <summary>
/// 支持惰性解析的可变内联元素集合。
/// 原始文本在缓冲区中累积；内联解析延迟到
/// 渲染器调用 <see cref="GetInlines"/> 或
/// 解析器调用 <see cref="Seal"/> 时执行。
/// </summary>
public sealed class InlineCollection : INotifyCollectionChanged
{
    private readonly StringBuilder _rawBuffer = new();
    private IReadOnlyList<InlineElement>? _parsed;
    private bool _dirty;

    /// <summary>
    /// 获取原始文本缓冲区（供解析器使用）。
    /// </summary>
    internal StringBuilder RawBuffer => _rawBuffer;

    /// <summary>
    /// 将原始文本追加到内联缓冲区中。
    /// 将解析状态标记为脏。
    /// </summary>
    public void Append(string text)
    {
        _rawBuffer.Append(text);
        _dirty = true;
        RaiseCollectionChanged();
    }

    /// <summary>
    /// 将换行符追加到内联缓冲区中。
    /// 将解析状态标记为脏。
    /// </summary>
    public void AppendLine()
    {
        _rawBuffer.Append('\n');
        _dirty = true;
        RaiseCollectionChanged();
    }

    /// <summary>
    /// 获取解析后的内联元素。
    /// 如果自上次调用以来缓冲区已更改，则惰性解析。
    /// </summary>
    public IReadOnlyList<InlineElement> GetInlines()
    {
        if (!_dirty && _parsed is not null)
            return _parsed;

        _parsed = ParseBuffer();
        _dirty = false;
        return _parsed;
    }

    /// <summary>
    /// 密封内联集合，对缓冲区执行最终解析。
    /// 当所属块定稿时由解析器调用。
    /// 此后，内容被视为已完成且不再可变。
    /// </summary>
    public void Seal()
    {
        _parsed = ParseBuffer();
        _dirty = false;
        RaiseCollectionChanged();
    }

    /// <summary>
    /// 使用 InlineParser 解析当前缓冲区内容。
    /// </summary>
    private IReadOnlyList<InlineElement> ParseBuffer()
    {
        if (_rawBuffer.Length == 0)
            return Array.Empty<InlineElement>();

        var parser = new InlineParser(_rawBuffer.ToString());
        return parser.Parse().AsReadOnly();
    }

    /// <summary>
    /// 当集合发生更改时触发。目前仅在 Seal 时触发。
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    private void RaiseCollectionChanged()
    {
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
