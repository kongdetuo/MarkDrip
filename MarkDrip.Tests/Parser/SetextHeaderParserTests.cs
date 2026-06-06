using MarkDrip.Model;
using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class SetextHeaderParserTests
{
    private static string GetInlinesText(InlineCollection inlines)
    {
        var elements = inlines.GetInlines();
        if (elements.Count == 0) return "";
        return string.Concat(elements.Select(ElementToText));
    }

    private static string ElementToText(InlineElement e) => e switch
    {
        TextRun t => t.Text,
        SoftLineBreak => "\n",
        LineBreak => "\n",
        _ => "",
    };

    /// <summary>创建一个包含 Open 段落块的上下文（模拟段落后紧跟 setext 下划线的状态）。</summary>
    private static ParserContext CreateContextWithOpenParagraph(string text)
    {
        var ctx = new ParserContext();
        var para = new ParagraphBlock();
        para.Inlines.Append(text);
        ctx.Blocks.Add(para);
        return ctx;
    }

    // ═══════════════════════════════════════════
    //  TryMatch
    // ═══════════════════════════════════════════

    [DataTestMethod]
    [DataRow("====\n")]
    [DataRow("----\n")]
    [DataRow("  ====\n")]
    [DataRow("\t====\n")]
    [DataRow("===\n")]
    [DataRow("--\n")]
    public void TryMatch_SetextUnderlineAfterParagraph_ReturnsFullMatch(string line)
    {
        var ctx = CreateContextWithOpenParagraph("some text");
        var parser = new SetextHeaderParser();

        var result = parser.TryMatch(line, ctx);

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [DataTestMethod]
    [DataRow("text\n")]
    [DataRow("==x\n")]
    [DataRow("-=-\n")]
    [DataRow("\n")]          // blank line
    [DataRow("  \n")]        // whitespace only
    public void TryMatch_InvalidUnderline_ReturnsNoMatch(string line)
    {
        var ctx = CreateContextWithOpenParagraph("some text");
        var parser = new SetextHeaderParser();

        var result = parser.TryMatch(line, ctx);

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    [TestMethod]
    public void TryMatch_NoPreviousParagraph_ReturnsNoMatch()
    {
        var ctx = new ParserContext(); // no blocks
        var parser = new SetextHeaderParser();

        var result = parser.TryMatch("====\n", ctx);

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    [TestMethod]
    public void TryMatch_PreviousBlockFinalized_ReturnsNoMatch()
    {
        var ctx = new ParserContext();
        var para = new ParagraphBlock();
        para.Status = BlockStatus.Finalized;
        ctx.Blocks.Add(para);
        var parser = new SetextHeaderParser();

        var result = parser.TryMatch("====\n", ctx);

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    [TestMethod]
    public void TryMatch_AllEqualsNoNewline_ReturnsPartialMatch()
    {
        var ctx = CreateContextWithOpenParagraph("text");
        var parser = new SetextHeaderParser();

        var result = parser.TryMatch("====", ctx);

        Assert.AreEqual(MatchResult.PartialMatch, result);
    }

    // ═══════════════════════════════════════════
    //  OnMatch
    // ═══════════════════════════════════════════

    [TestMethod]
    public void OnMatch_Equals_ConvertsToLevel1Heading()
    {
        var ctx = CreateContextWithOpenParagraph("Hello");
        var parser = new SetextHeaderParser();

        parser.OnMatch("====\n", ctx);

        Assert.AreEqual(1, ctx.Blocks.Count);
        var heading = ctx.Blocks[0] as HeadingBlock;
        Assert.IsNotNull(heading);
        Assert.AreEqual(1, heading.Level);
        Assert.AreEqual("Hello", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void OnMatch_Dashes_ConvertsToLevel2Heading()
    {
        var ctx = CreateContextWithOpenParagraph("World");
        var parser = new SetextHeaderParser();

        parser.OnMatch("----\n", ctx);

        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual(2, heading.Level);
        Assert.AreEqual("World", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void OnMatch_WithLeadingSpaces_StillConvertsCorrectly()
    {
        var ctx = CreateContextWithOpenParagraph("Indented heading");
        var parser = new SetextHeaderParser();

        parser.OnMatch("  ====\n", ctx);

        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual(1, heading.Level);
        Assert.AreEqual("Indented heading", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void OnMatch_MultiLineParagraph_PreservesSoftBreaks()
    {
        var ctx = new ParserContext();
        var para = new ParagraphBlock();
        para.Inlines.Append("First line\nSecond line");
        ctx.Blocks.Add(para);
        var parser = new SetextHeaderParser();

        parser.OnMatch("----\n", ctx);

        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual(2, heading.Level);
        Assert.AreEqual("First line\nSecond line", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void OnMatch_ReplacesParagraph_BlocksCountSame()
    {
        var ctx = CreateContextWithOpenParagraph("test");
        int beforeCount = ctx.Blocks.Count;
        var parser = new SetextHeaderParser();

        parser.OnMatch("====\n", ctx);

        Assert.AreEqual(beforeCount, ctx.Blocks.Count);
        Assert.IsInstanceOfType(ctx.Blocks[0], typeof(HeadingBlock));
    }

    // ═══════════════════════════════════════════
    //  Feed integration (via StreamParser)
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_SingleLineSetext_Level1()
    {
        var parser = new StreamParser();

        parser.Feed("Hello\n====\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var heading = (HeadingBlock)parser.Blocks[0];
        Assert.AreEqual(1, heading.Level);
        Assert.AreEqual("Hello", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Feed_SingleLineSetext_Level2()
    {
        var parser = new StreamParser();

        parser.Feed("World\n----\n");

        var heading = (HeadingBlock)parser.Blocks[0];
        Assert.AreEqual(2, heading.Level);
        Assert.AreEqual("World", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Feed_MultiLineParagraphSetext()
    {
        var parser = new StreamParser();

        parser.Feed("First\nsecond\n----\n");

        var heading = (HeadingBlock)parser.Blocks[0];
        Assert.AreEqual(2, heading.Level);
        Assert.AreEqual("First\nsecond", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Feed_SetextNotTriggeredWithoutParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("====\n");
        // No preceding paragraph → should be treated as paragraph content
        var block = parser.Blocks[0];
        Assert.IsInstanceOfType(block, typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Feed_ParagraphThenSetextThenParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("Heading text\n====\n\nNormal paragraph\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(HeadingBlock));
        Assert.AreEqual(1, ((HeadingBlock)parser.Blocks[0]).Level);
        Assert.AreEqual("Heading text", GetInlinesText(((HeadingBlock)parser.Blocks[0]).Inlines));
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Feed_SetextLevel2AfterTwoLineParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("Some text here\nmore text\n----\n");

        var heading = (HeadingBlock)parser.Blocks[0];
        Assert.AreEqual(2, heading.Level);
        Assert.AreEqual("Some text here\nmore text", GetInlinesText(heading.Inlines));
    }
}
