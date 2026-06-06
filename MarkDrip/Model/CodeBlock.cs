using System.Text;

namespace MarkDrip.Model;

/// <summary>
/// 表示围栏式或缩进式代码块。
/// 内容以原始文本存储（不进行内联解析）。
/// </summary>
public sealed class CodeBlock : DocumentBlock
{
    private string? _infoString;

    /// <summary>
    /// 获取或设置开头围栏的信息字符串（语言标识符）。
    /// 例如：在 ```csharp 中，信息字符串为 "csharp"。
    /// </summary>
    public string? InfoString
    {
        get => _infoString;
        internal set
        {
            if (_infoString == value) return;
            _infoString = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 获取此代码块的可变原始内容缓冲区。
    /// </summary>
    public StringBuilder Content { get; } = new();

    /// <summary>围栏宽度（解析器使用）。</summary>
    internal int OpenFenceLength { get; set; }

    /// <summary>围栏字符（解析器使用）。</summary>
    internal char OpenFenceChar { get; set; }

    /// <summary>流式模式下开围栏的信息字符串尚未到达，Append 应暂不转发到 Content。</summary>
    internal bool InfoStringPending { get; set; }

    /// <summary>
    /// 流式追加原始内容片断并通知变更。适用于逐字符/逐块的流式输入。
    /// </summary>
    internal void AppendContent(ReadOnlySpan<char> chunk)
    {
        Content.Append(chunk);
        NotifyContentChanged();
    }

    /// <summary>
    /// 初始化 <see cref="CodeBlock"/> 的新实例。
    /// </summary>
    public CodeBlock()
    {
        Kind = BlockKind.CodeBlock;
    }
}
