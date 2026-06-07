using MarkDrip.Model;
using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class CodeBlockParserTests
{
    // ═══════════════════════════════════════════
    //  TryMatch
    // ═══════════════════════════════════════════

    [DataTestMethod]
    [DataRow("```\n")]
    [DataRow("```cs\n")]
    [DataRow("````\n")]
    [DataRow("~~~\n")]
    [DataRow("~~~~~~\n")]
    [DataRow(" ~~~\n")]
    [DataRow("  ~~~\n")]
    [DataRow("   ~~~\n")]
    [DataRow("   ```javascript\n")]
    public void TryMatch_Fence_ReturnsFullMatch(string line)
    {
        var parser = new CodeBlockParser();

        var result = parser.TryMatch(line, new ParserContext());

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [DataTestMethod]
    [DataRow("``\n")]       // 2 backticks only
    [DataRow("~~\n")]       // 2 tildes only
    [DataRow("text\n")]
    [DataRow("> text\n")]
    [DataRow("    ```\n")]  // 4 spaces → indented code, not fenced
    [DataRow("")]           // no newline either, but empty
    public void TryMatch_NotFence_ReturnsNoMatch(string line)
    {
        var parser = new CodeBlockParser();

        var result = parser.TryMatch(line, new ParserContext());

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    // ═══════════════════════════════════════════
    //  OnMatch
    // ═══════════════════════════════════════════

    [TestMethod]
    public void OnMatch_CreatesCodeBlock()
    {
        var parser = new CodeBlockParser();
        var ctx = new ParserContext();

        parser.OnMatch("```\n", ctx);

        Assert.AreEqual(1, ctx.Blocks.Count);
        Assert.IsInstanceOfType(ctx.Blocks[0], typeof(CodeBlock));
    }

    [TestMethod]
    public void OnMatch_ParsesInfoString()
    {
        var parser = new CodeBlockParser();
        var buf = new LineBuffer();
        buf.Append("```csharp\n");
        var ctx = new ParserContext(new LineBufferView(buf));

        parser.OnMatch("```csharp\n", ctx);
        parser.Append(new TextChuck("```csharp\n", false, false), ctx);

        var code = (CodeBlock)ctx.Blocks[0];
        Assert.AreEqual("csharp", code.InfoString);
    }

    [TestMethod]
    public void OnMatch_TildeFence_ParsesInfoString()
    {
        var parser = new CodeBlockParser();
        var buf = new LineBuffer();
        buf.Append("~~~ python\n");
        var ctx = new ParserContext(new LineBufferView(buf));

        parser.OnMatch("~~~ python\n", ctx);
        parser.Append(new TextChuck("~~~ python\n", false, false), ctx);

        var code = (CodeBlock)ctx.Blocks[0];
        Assert.AreEqual("python", code.InfoString);
    }

    [TestMethod]
    public void OnMatch_BacktickInInfoString_RejectsInfo()
    {
        var parser = new CodeBlockParser();
        var buf = new LineBuffer();
        buf.Append("```a`b\n");
        var ctx = new ParserContext(new LineBufferView(buf));

        parser.OnMatch("```a`b\n", ctx);
        parser.Append(new TextChuck("```a`b\n", false, false), ctx);

        var code = (CodeBlock)ctx.Blocks[0];
        Assert.IsNull(code.InfoString, "Backticks in info string should be rejected");
    }

    // ═══════════════════════════════════════════
    //  Feed integration
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_FencedCodeBlock_SingleLine()
    {
        var parser = new StreamParser();

        parser.Feed("```\ncode\n```\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("code", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_FencedCodeBlock_MultiLine()
    {
        var parser = new StreamParser();

        parser.Feed("```\nline1\nline2\nline3\n```\n");

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("line1\nline2\nline3", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_FencedCodeBlock_WithLanguage()
    {
        var parser = new StreamParser();

        parser.Feed("```python\nprint('hello')\n```\n");

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("python", code.InfoString);
        Assert.AreEqual("print('hello')", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_FencedCodeBlock_LongerClosingFence()
    {
        var parser = new StreamParser();

        parser.Feed("```\ncode\n````\n");

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("code", code.Content.ToString());
        Assert.AreEqual(BlockStatus.Finalized, code.Status);
    }

    [TestMethod]
    public void Feed_FencedCodeBlock_TildeFence()
    {
        var parser = new StreamParser();

        parser.Feed("~~~\ncode\n~~~\n");

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("code", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_UnclosedCodeBlock_RemainsAsBlock()
    {
        var parser = new StreamParser();

        parser.Feed("```\ncode\n");

        // EOF 时仍保留代码块
        parser.Complete();

        Assert.AreEqual(1, parser.Blocks.Count);
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("code", code.Content.ToString());
        Assert.AreEqual(BlockStatus.Finalized, code.Status);
    }

    [TestMethod]
    public void Feed_CodeBlockThenParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("```\ncode\n```\n\nparagraph\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(CodeBlock));
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Feed_BacktickInContent_NotClosing()
    {
        var parser = new StreamParser();

        parser.Feed("```\n`not a closing fence\n```\n");

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("`not a closing fence", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_BacktickFence_ClosingWithText_NotClosing()
    {
        var parser = new StreamParser();

        parser.Feed("```\ncode\n``` more text\n");
        parser.Complete();

        var code = (CodeBlock)parser.Blocks[0];
        // ``` 后还有文字 → 不是合法的闭围栏（反引号围栏要求闭围栏后只有空白）
        // 所以整个内容都在代码块中
        Assert.IsNull(code.InfoString);
        Assert.AreEqual("code\n``` more text", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_TildeFence_ClosingWithText_StillCloses()
    {
        var parser = new StreamParser();

        parser.Feed("~~~\ncode\n~~~ more text\n");

        var code = (CodeBlock)parser.Blocks[0];
        // ~~~ 后可以跟任意文本（CommonMark 波浪线规则）
        Assert.AreEqual("code", code.Content.ToString());
        Assert.AreEqual(BlockStatus.Finalized, code.Status);
    }

    [TestMethod]
    public void Feed_CodeBlock_ContentWithBlankLines()
    {
        var parser = new StreamParser();

        parser.Feed("```\ncode\n\nmore\n```\n");

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("code\n\nmore", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_CodeBlock_LeadingSpacesOnFence()
    {
        var parser = new StreamParser();

        parser.Feed("  ```\nindented code\n  ```\n");

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("indented code", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_CodeBlock_PreservesLeadingSpacesInContent()
    {
        var parser = new StreamParser();

        parser.Feed("```\n    indented content\n```\n");

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("    indented content", code.Content.ToString());
    }
}
