namespace MarkDrip.Model;

/// <summary>
/// 所有 Markdown 内联元素的基类型。
/// </summary>
public abstract record InlineElement;

/// <summary>
/// 一段无格式的纯文本。
/// </summary>
/// <param name="Text">文本内容。</param>
public sealed record TextRun(string Text) : InlineElement;

/// <summary>
/// 强调文本（斜体，渲染为 *text* 或 _text_）。
/// </summary>
/// <param name="Children">强调范围内的内联元素。</param>
public sealed record Emphasis(IReadOnlyList<InlineElement> Children) : InlineElement;

/// <summary>
/// 强烈强调文本（粗体，渲染为 **text** 或 __text__）。
/// </summary>
/// <param name="Children">强烈强调范围内的内联元素。</param>
public sealed record StrongEmphasis(IReadOnlyList<InlineElement> Children) : InlineElement;

/// <summary>
/// 内联代码范围（渲染为 `code`）。
/// </summary>
/// <param name="Code">原始代码内容。</param>
public sealed record InlineCode(string Code) : InlineElement;

/// <summary>
/// 超链接（[text](url)）。
/// </summary>
/// <param name="Url">目标 URL。</param>
/// <param name="Title">可选的标题属性。</param>
/// <param name="Children">链接文本的内联元素。</param>
public sealed record Link(string Url, string? Title, IReadOnlyList<InlineElement> Children) : InlineElement;

/// <summary>
/// 图片（![alt](url)）。
/// </summary>
/// <param name="Url">图片 URL。</param>
/// <param name="Alt">替代文本描述。</param>
public sealed record Image(string Url, string? Alt) : InlineElement;

/// <summary>
/// 硬换行（渲染为 \<br\> 或两个尾随空格 + 换行）。
/// </summary>
public sealed record LineBreak : InlineElement;

/// <summary>
/// 段落内的软换行（渲染为空格或换行）。
/// </summary>
public sealed record SoftLineBreak : InlineElement;
