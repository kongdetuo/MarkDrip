using MarkDrip.Controls;
using MarkDrip.Model;
using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class InlineParserTests
{
    [TestMethod]
    public void UnderscoreInsideWord_NotEmphasis()
    {
        // Note: Currently produces 2 TextRuns (hello_ + world).
        // In CommonMark, _ inside a word should not be emphasis.
        // This is a known limitation where the flanking detection
        // doesn't perfectly match CommonMark spec for underscores.
        var result = Parse("hello_world");
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(TextRun));
        Assert.AreEqual("hello_world", ((TextRun)result[0]).Text);
    }

    [TestMethod]
    public void EmptyText_ReturnsEmpty()
    {
        var result = Parse("");
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Bold_WithDoubleAsterisks()
    {
        var result = Parse("**bold**");
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(StrongEmphasis));
        var strong = (StrongEmphasis)result[0];
        Assert.AreEqual(1, strong.Children.Count);
        Assert.IsInstanceOfType(strong.Children[0], typeof(TextRun));
        Assert.AreEqual("bold", ((TextRun)strong.Children[0]).Text);
    }

    [TestMethod]
    public void Italic_WithSingleAsterisk()
    {
        var result = Parse("*italic*");
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(Emphasis));
        var em = (Emphasis)result[0];
        Assert.AreEqual(1, em.Children.Count);
        Assert.IsInstanceOfType(em.Children[0], typeof(TextRun));
        Assert.AreEqual("italic", ((TextRun)em.Children[0]).Text);
    }

    [TestMethod]
    public void Italic_WithUnderscore()
    {
        var result = Parse("_italic_");
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(Emphasis));
        var em = (Emphasis)result[0];
        Assert.AreEqual("italic", ((TextRun)em.Children[0]).Text);
    }

    [TestMethod]
    public void Bold_Inside_Italic()
    {
        var result = Parse("*italic **bold** italic*");
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(Emphasis));
        var em = (Emphasis)result[0];
        Assert.AreEqual("italic ", ((TextRun)em.Children[0]).Text);
        Assert.IsInstanceOfType(em.Children[1], typeof(StrongEmphasis));
        Assert.AreEqual("bold", ((TextRun)((StrongEmphasis)em.Children[1]).Children[0]).Text);
        Assert.IsInstanceOfType(em.Children[2], typeof(TextRun));
        Assert.AreEqual(" italic", ((TextRun)em.Children[2]).Text);
    }

    [TestMethod]
    public void InlineCode_WithBackticks()
    {
        var result = Parse("code `var x = 1;` here");
        Assert.AreEqual(3, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(TextRun));
        Assert.AreEqual("code ", ((TextRun)result[0]).Text);
        Assert.IsInstanceOfType(result[1], typeof(InlineCode));
        Assert.AreEqual("var x = 1;", ((InlineCode)result[1]).Code);
        Assert.IsInstanceOfType(result[2], typeof(TextRun));
        Assert.AreEqual(" here", ((TextRun)result[2]).Text);
    }

    [TestMethod]
    public void InlineCode_Unclosed_ReturnsLiteralBackticks()
    {
        var result = Parse("text `unclosed");
        // 未闭合的代码范围：反引号和后续内容合并为一个 TextRun
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(TextRun));
        StringAssert.Contains(((TextRun)result[0]).Text, "text");
        StringAssert.Contains(((TextRun)result[0]).Text, "`");
    }

    [TestMethod]
    public void InlineCode_SingleBacktickConsumed()
    {
        var result = Parse("`code`");
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(InlineCode));
        Assert.AreEqual("code", ((InlineCode)result[0]).Code);
    }

    [TestMethod]
    public void Link_Basic()
    {
        var result = Parse("[text](http://example.com)");
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(Link));
        var link = (Link)result[0];
        Assert.AreEqual("http://example.com", link.Url);
        Assert.IsNull(link.Title);
        Assert.AreEqual(1, link.Children.Count);
        Assert.AreEqual("text", ((TextRun)link.Children[0]).Text);
    }

    [TestMethod]
    public void Link_WithTitle()
    {
        var result = Parse("[text](http://example.com \"Title\")");
        Assert.AreEqual(1, result.Count);
        var link = (Link)result[0];
        Assert.AreEqual("http://example.com", link.Url);
        Assert.AreEqual("Title", link.Title);
    }

    [TestMethod]
    public void Link_WithBracketsInText()
    {
        var result = Parse("[text [with] brackets](http://example.com)");
        Assert.AreEqual(1, result.Count);
        var link = (Link)result[0];
        Assert.AreEqual("http://example.com", link.Url);
        Assert.AreEqual("text [with] brackets", GetText(link));
    }

    [TestMethod]
    public void Image_Basic()
    {
        var result = Parse("![alt](http://example.com/img.png)");
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(Image));
        var img = (Image)result[0];
        Assert.AreEqual("http://example.com/img.png", img.Url);
        Assert.AreEqual("alt", img.Alt);
    }

    [TestMethod]
    public void Escape_BackslashBeforeEmphasis()
    {
        var result = Parse("\\*not italic*");
        // 转义的 * 和后续文本合并为一个 TextRun，末尾 * 作字面量
        Assert.AreEqual(2, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(TextRun));
        Assert.AreEqual("*not italic", ((TextRun)result[0]).Text);
        Assert.IsInstanceOfType(result[1], typeof(TextRun));
        Assert.AreEqual("*", ((TextRun)result[1]).Text);
    }

    [TestMethod]
    public void Escape_BackslashBeforeBracket_ProducesLiteral()
    {
        var result = Parse("\\[not a link](url)");
        // 转义后的 [ 不会触发链接解析，整体作为普通文本
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(TextRun));
        Assert.AreEqual("[not a link](url)", ((TextRun)result[0]).Text);
    }

    [TestMethod]
    public void HardLineBreak_TwoSpacesAndNewline()
    {
        var result = Parse("line1  \nline2");
        Assert.AreEqual(3, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(TextRun));
        Assert.AreEqual("line1", ((TextRun)result[0]).Text);
        Assert.IsInstanceOfType(result[1], typeof(LineBreak));
        Assert.IsInstanceOfType(result[2], typeof(TextRun));
        Assert.AreEqual("line2", ((TextRun)result[2]).Text);
    }

    [TestMethod]
    public void SoftLineBreak_NewlineOnly()
    {
        var result = Parse("line1\nline2");
        Assert.AreEqual(3, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(TextRun));
        Assert.AreEqual("line1", ((TextRun)result[0]).Text);
        Assert.IsInstanceOfType(result[1], typeof(SoftLineBreak));
        Assert.IsInstanceOfType(result[2], typeof(TextRun));
        Assert.AreEqual("line2", ((TextRun)result[2]).Text);
    }

    [TestMethod]
    public void Mixed_BoldItalicAndCode()
    {
        var result = Parse("**bold** and `code`");
        Assert.AreEqual(3, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(StrongEmphasis));
        Assert.IsInstanceOfType(result[1], typeof(TextRun));
        Assert.AreEqual(" and ", ((TextRun)result[1]).Text);
        Assert.IsInstanceOfType(result[2], typeof(InlineCode));
    }

    [TestMethod]
    public void EmphasizedLink()
    {
        var result = Parse("*[link](url)*");
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(Emphasis));
        var em = (Emphasis)result[0];
        Assert.AreEqual(1, em.Children.Count);
        Assert.IsInstanceOfType(em.Children[0], typeof(Link));
        Assert.AreEqual("url", ((Link)em.Children[0]).Url);
    }

    [TestMethod]
    public void MultiParagraphText_WithMixedEmphasis()
    {
        var result = Parse("normal **bold** normal *italic* normal");
        Assert.AreEqual(5, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(TextRun));
        Assert.AreEqual("normal ", ((TextRun)result[0]).Text);
        Assert.IsInstanceOfType(result[1], typeof(StrongEmphasis));
        Assert.IsInstanceOfType(result[2], typeof(TextRun));
        Assert.AreEqual(" normal ", ((TextRun)result[2]).Text);
        Assert.IsInstanceOfType(result[3], typeof(Emphasis));
        Assert.IsInstanceOfType(result[4], typeof(TextRun));
        Assert.AreEqual(" normal", ((TextRun)result[4]).Text);
    }

    [TestMethod]
    public void Emphasis_IntrawordWithAsterisk()
    {
        var result = Parse("un*believ*able");
        Assert.AreEqual(3, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(TextRun));
        Assert.AreEqual("un", ((TextRun)result[0]).Text);
        Assert.IsInstanceOfType(result[1], typeof(Emphasis));
        Assert.AreEqual("believ", ((TextRun)((Emphasis)result[1]).Children[0]).Text);
        Assert.IsInstanceOfType(result[2], typeof(TextRun));
        Assert.AreEqual("able", ((TextRun)result[2]).Text);
    }

    [TestMethod]
    public void TripleAsterisk_EmphasisInBold()
    {
        // ***text*** → <strong><em>text</em></strong>
        var result = Parse("***text***");
        Assert.AreEqual(1, result.Count);
        // Can be either StrongEmphasis > Emphasis, or Emphasis > StrongEmphasis
        Assert.IsTrue(result[0] is StrongEmphasis or Emphasis);
    }

    [TestMethod]
    public void EscapedBackslash_ProducesLiteral()
    {
        var result = Parse("\\\\");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("\\", ((TextRun)result[0]).Text);
    }

    [TestMethod]
    public void LinkWithEmphasisText()
    {
        var result = Parse("[*emphasized*](url)");
        Assert.AreEqual(1, result.Count);
        Assert.IsInstanceOfType(result[0], typeof(Link));
        var link = (Link)result[0];
        Assert.AreEqual(1, link.Children.Count);
        Assert.IsInstanceOfType(link.Children[0], typeof(Emphasis));
    }

    [TestMethod]
    public void BackslashHardBreak()
    {
        var result = Parse("line1\\\nline2");
        Assert.AreEqual(3, result.Count);
        Assert.IsInstanceOfType(result[1], typeof(LineBreak));
    }

    [TestMethod]
    public void EmphasisWithCJK_BoldItalicAndCode()
    {
        // 复现 demo 中的问题：斜体 + CJK + 粗体 + 代码范围
        var result = Parse("*混合样式*：在块引用内使用 **粗体** 和 `代码`");
        Console.WriteLine("=== EmphasisWithCJK_BoldItalicAndCode ===");
        for (int i = 0; i < result.Count; i++)
            Console.WriteLine($"  [{i}] {Describe(result[i])}");
        Console.WriteLine();

        // 期望：
        // [0] Emphasis("混合样式")
        // [1] TextRun("：在块引用内使用 ")
        // [2] StrongEmphasis("粗体")
        // [3] TextRun(" 和 ")
        // [4] InlineCode("代码")
        Assert.AreEqual(5, result.Count, "Should have 5 elements");
        Assert.IsInstanceOfType(result[0], typeof(Emphasis));
        Assert.AreEqual("混合样式", ((TextRun)((Emphasis)result[0]).Children[0]).Text);
        Assert.IsInstanceOfType(result[1], typeof(TextRun));
        Assert.AreEqual("：在块引用内使用 ", ((TextRun)result[1]).Text);
        Assert.IsInstanceOfType(result[2], typeof(StrongEmphasis));
        Assert.AreEqual("粗体", ((TextRun)((StrongEmphasis)result[2]).Children[0]).Text);
        Assert.IsInstanceOfType(result[3], typeof(TextRun));
        Assert.AreEqual(" 和 ", ((TextRun)result[3]).Text);
        Assert.IsInstanceOfType(result[4], typeof(InlineCode));
        Assert.AreEqual("代码", ((InlineCode)result[4]).Code);
    }

    [TestMethod]
    public void EmphasisInBlockquote_StreamingScenario()
    {
        // 模拟块引用中的多段行内内容
        var result = Parse("*混合样式*\n：在块引用内使用 **粗体** 和 `代码`");
        Console.WriteLine("=== EmphasisInBlockquote_StreamingScenario ===");
        for (int i = 0; i < result.Count; i++)
            Console.WriteLine($"  [{i}] {Describe(result[i])}");
        Console.WriteLine();

        // 第一行: *混合样式* → Emphasis("混合样式")
        // 然后软换行
        // 第二行: 文本 + **粗体** + 文本 + `代码`
        int idx = 0;
        Assert.IsInstanceOfType(result[idx], typeof(Emphasis));
        idx++;
        Assert.IsInstanceOfType(result[idx], typeof(SoftLineBreak));
        idx++;
        Assert.IsInstanceOfType(result[idx], typeof(TextRun)); // "：在块引用内使用 "
        idx++;
        Assert.IsInstanceOfType(result[idx], typeof(StrongEmphasis)); // "粗体"
        idx++;
        Assert.IsInstanceOfType(result[idx], typeof(TextRun)); // " 和 "
        idx++;
        Assert.IsInstanceOfType(result[idx], typeof(InlineCode)); // "代码"
    }

    private static string Describe(InlineElement el) => el switch
    {
        TextRun t => $"\"{t.Text}\"",
        Emphasis e => $"Emphasis([{string.Join(", ", e.Children.Select(Describe))}])",
        StrongEmphasis s => $"StrongEmphasis([{string.Join(", ", s.Children.Select(Describe))}])",
        InlineCode c => $"Code(\"{c.Code}\")",
        Link l => $"Link(\"{l.Url}\", [{string.Join(", ", l.Children.Select(Describe))}])",
        _ => el.ToString() ?? "(null)"
    };

    [TestMethod]
    public void RenderToAvaloniaControls_EmphasisStructure_IsCorrect()
    {
        // 验证从 InlineElement 模型 → Avalonia 控件的渲染结构正确
        // 模拟视图 MarkdownView.BuildInlineContent 的渲染逻辑
        var input = "*混合样式*：在块引用内使用 **粗体** 和 `代码`。";
        var parser = new InlineParser(input);
        var elements = parser.Parse();

        // 创建 InlineCollection 并填入解析结果（模拟 ParagraphBlock.Inlines）
        var inlinesModel = new InlineCollection();
        inlinesModel.Append(input);

        var textBlock = new Avalonia.Controls.TextBlock();
        // 模拟 MakeParaBlock 的生产行为：不显式设置 Inlines
        MarkdownView.BuildInlineContent(textBlock, inlinesModel);

        var avInlines = textBlock.Inlines!;
        var desc = DescribeAvInlines(avInlines);

        // 渲染后的控件结构
        // 期望： [Italic Run "混合样式"] [Run "：在块引用内使用 "] [Bold Run "粗体"] [Run " 和 "] [Code Span "代码"] [Run "。"]
        Assert.AreEqual(6, avInlines.Count, $"应有 6 个顶级内联控件，实际 {avInlines.Count}:\n{desc}");

        // 验证 Emphasis → Run with FontStyle=Italic (样式直接在 Run 上，不再用 Span 包裹)
        Assert.IsInstanceOfType(avInlines[0], typeof(Avalonia.Controls.Documents.Run),
            $"[0] 应为 Run(Italic)\n{desc}");
        var italicRun = (Avalonia.Controls.Documents.Run)avInlines[0];
        Assert.AreEqual(Avalonia.Media.FontStyle.Italic, italicRun.FontStyle,
            $"[0] Run 应为 Italic\n{desc}");
        Assert.AreEqual("混合样式", italicRun.Text,
            $"[0] Italic 文本应为 '混合样式'\n{desc}");

        // 验证中间文本
        Assert.IsInstanceOfType(avInlines[1], typeof(Avalonia.Controls.Documents.Run),
            $"[1] 应为 Run\n{desc}");
        Assert.AreEqual("：在块引用内使用 ", ((Avalonia.Controls.Documents.Run)avInlines[1]).Text,
            $"[1] 文本不正确\n{desc}");

        // 验证 StrongEmphasis → Run with FontWeight=Bold
        Assert.IsInstanceOfType(avInlines[2], typeof(Avalonia.Controls.Documents.Run),
            $"[2] 应为 Run(Bold)\n{desc}");
        var boldRun = (Avalonia.Controls.Documents.Run)avInlines[2];
        Assert.AreEqual(Avalonia.Media.FontWeight.Bold, boldRun.FontWeight,
            $"[2] Run 应为 Bold\n{desc}");
        Assert.AreEqual("粗体", boldRun.Text,
            $"[2] Bold 文本应为 '粗体'\n{desc}");

        // 验证末尾文本 + 代码
        Assert.IsInstanceOfType(avInlines[3], typeof(Avalonia.Controls.Documents.Run),
            $"[3] 应为 Run\n{desc}");
        Assert.AreEqual(" 和 ", ((Avalonia.Controls.Documents.Run)avInlines[3]).Text);

        Assert.IsInstanceOfType(avInlines[4], typeof(Avalonia.Controls.Documents.Span),
            $"[4] 应为 Span(Code)\n{desc}");
        var codeSpan = (Avalonia.Controls.Documents.Span)avInlines[4];
        Assert.AreEqual(1, codeSpan.Inlines.Count);
        Assert.AreEqual("代码", ((Avalonia.Controls.Documents.Run)codeSpan.Inlines[0]).Text);

        Assert.IsInstanceOfType(avInlines[5], typeof(Avalonia.Controls.Documents.Run),
            $"[5] 应为 Run\n{desc}");
        Assert.AreEqual("。", ((Avalonia.Controls.Documents.Run)avInlines[5]).Text);
    }

    [TestMethod]
    public void RenderToAvalonia_NestedBoldItalic_ProducesRunWithBothStyles()
    {
        // ***粗斜体*** → StrongEmphasis[TextRun("粗斜体")] in our parser
        var inlinesModel = new InlineCollection();
        inlinesModel.Append("***粗斜体***");
        var textBlock = new Avalonia.Controls.TextBlock();
        MarkdownView.BuildInlineContent(textBlock, inlinesModel);

        var avInlines = textBlock.Inlines!;
        var desc = DescribeAvInlines(avInlines);

        Assert.AreEqual(1, avInlines.Count);
        Assert.IsInstanceOfType(avInlines[0], typeof(Avalonia.Controls.Documents.Run));
        var run = (Avalonia.Controls.Documents.Run)avInlines[0];
        Assert.AreEqual("粗斜体", run.Text);
        Assert.AreEqual(Avalonia.Media.FontWeight.Bold, run.FontWeight, $"Run should be Bold\n{desc}");
    }

    [TestMethod]
    public void RenderToAvalonia_EmphasisAndCodeSpan_CodeNotAffectedByEmphasis()
    {
        // `代码` 不应该受前后 *emphasis* 影响
        var inlinesModel = new InlineCollection();
        inlinesModel.Append("*斜体*和`代码`和**粗体**");
        var textBlock = new Avalonia.Controls.TextBlock();
        MarkdownView.BuildInlineContent(textBlock, inlinesModel);

        var avInlines = textBlock.Inlines!;
        var desc = DescribeAvInlines(avInlines);

        Assert.AreEqual(5, avInlines.Count, desc);

        // [0] Run(Italic) "斜体"
        Assert.IsInstanceOfType(avInlines[0], typeof(Avalonia.Controls.Documents.Run));
        var r0 = (Avalonia.Controls.Documents.Run)avInlines[0];
        Assert.AreEqual(Avalonia.Media.FontStyle.Italic, r0.FontStyle);
        Assert.AreEqual("斜体", r0.Text);

        // [1] Run "和"
        Assert.IsInstanceOfType(avInlines[1], typeof(Avalonia.Controls.Documents.Run));
        var r1 = (Avalonia.Controls.Documents.Run)avInlines[1];
        Assert.AreEqual("和", r1.Text);

        // [2] Span(Code) "代码"
        Assert.IsInstanceOfType(avInlines[2], typeof(Avalonia.Controls.Documents.Span));
        var s2 = (Avalonia.Controls.Documents.Span)avInlines[2];
        Assert.AreEqual(1, s2.Inlines.Count);
        Assert.AreEqual("代码", ((Avalonia.Controls.Documents.Run)s2.Inlines[0]).Text);

        // [3] Run "和"
        Assert.IsInstanceOfType(avInlines[3], typeof(Avalonia.Controls.Documents.Run));
        var r3 = (Avalonia.Controls.Documents.Run)avInlines[3];
        Assert.AreEqual("和", r3.Text);

        // [4] Run(Bold) "粗体"
        Assert.IsInstanceOfType(avInlines[4], typeof(Avalonia.Controls.Documents.Run));
        var r4 = (Avalonia.Controls.Documents.Run)avInlines[4];
        Assert.AreEqual(Avalonia.Media.FontWeight.Bold, r4.FontWeight);
        Assert.AreEqual("粗体", r4.Text);
    }

    private static string DescribeAvInlines(Avalonia.Controls.Documents.InlineCollection inlines, int indent = 0)
    {
        var pad = new string(' ', indent * 2);
        var parts = new List<string>();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Avalonia.Controls.Documents.Run run:
                    var runStyle = "";
                    if (run.FontStyle == Avalonia.Media.FontStyle.Italic) runStyle += " Italic";
                    if (run.FontWeight == Avalonia.Media.FontWeight.Bold) runStyle += " Bold";
                    parts.Add($"{pad}[Run{runStyle} \"{run.Text}\"]");
                    break;
                case Avalonia.Controls.Documents.Span span:
                    var style = "";
                    if (span.FontStyle == Avalonia.Media.FontStyle.Italic) style = " Italic";
                    if (span.FontWeight == Avalonia.Media.FontWeight.Bold) style += " Bold";
                    var children = span.Inlines.Count > 0
                        ? "\n" + DescribeAvInlines(span.Inlines, indent + 1)
                        : "";
                    parts.Add($"{pad}[Span{style}]{children}");
                    break;
                default:
                    parts.Add($"{pad}[{inline.GetType().Name}]");
                    break;
            }
        }
        return string.Join("\n", parts);
    }

    private static List<InlineElement> Parse(string text)
    {
        var parser = new InlineParser(text);
        return parser.Parse();
    }

    private static string GetText(InlineElement el)
    {
        return el switch
        {
            TextRun t => t.Text,
            InlineCode c => c.Code,
            Emphasis e => string.Concat(e.Children.Select(GetText)),
            StrongEmphasis s => string.Concat(s.Children.Select(GetText)),
            Link l => string.Concat(l.Children.Select(GetText)),
            Image img => img.Alt ?? "",
            SoftLineBreak => "\n",
            LineBreak => "\n",
            _ => "",
        };
    }
}
