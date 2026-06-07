using MarkDrip.Model;
using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class AtxHeaderParserTests
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

    // ═══════════════════════════════════════════
    //  TryMatch
    // ═══════════════════════════════════════════

    [DataTestMethod]
    [DataRow("# ")]
    [DataRow("## ")]
    [DataRow("### ")]
    [DataRow("#### ")]
    [DataRow("##### ")]
    [DataRow("###### ")]
    public void TryMatch_StandardHeading_ReturnsFullMatch(string line)
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();

        var result = parser.TryMatch(new TextChunk(line, false), ctx);

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [DataTestMethod]
    [DataRow("#\t")]
    [DataRow("## \t")]
    public void TryMatch_TabAfterHash_ReturnsFullMatch(string line)
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();

        var result = parser.TryMatch(new TextChunk(line, false), ctx);

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("text")]
    [DataRow("no hash")]
    [DataRow(" # leading space")]
    public void TryMatch_NoLeadingHash_ReturnsNoMatch(string line)
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();

        var result = parser.TryMatch(new TextChunk(line, false), ctx);

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    [DataTestMethod]
    [DataRow("#text")]
    [DataRow("##text")]
    [DataRow("######text")]
    public void TryMatch_NoSpaceAfterHash_ReturnsNoMatch(string line)
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();

        var result = parser.TryMatch(new TextChunk(line, false), ctx);

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    [DataTestMethod]
    [DataRow("####### ")]
    [DataRow("####### text")]
    [DataRow("########")]
    public void TryMatch_MoreThanSixHashes_ReturnsNoMatch(string line)
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();

        var result = parser.TryMatch(new TextChunk(line, false), ctx);

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    [DataTestMethod]
    [DataRow("#")]
    [DataRow("##")]
    [DataRow("######")]
    public void TryMatch_OnlyHashes_ReturnsPartialMatch(string line)
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();

        var result = parser.TryMatch(new TextChunk(line, false), ctx);

        Assert.AreEqual(MatchResult.PartialMatch, result);
    }

    [DataTestMethod]
    [DataRow("# ")]
    [DataRow("## text")]
    [DataRow("### heading 3")]
    public void TryMatch_CrLfInLine_StillFullMatch(string line)
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();

        var result = parser.TryMatch(new TextChunk(line + "\n", false), ctx);

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    // ═══════════════════════════════════════════
    //  OnNewLineArrival
    // ═══════════════════════════════════════════

    [DataTestMethod]
    [DataRow("#", 1)]
    [DataRow("## ", 2)]
    [DataRow("### heading", 3)]
    [DataRow("#### heading", 4)]
    [DataRow("##### heading", 5)]
    [DataRow("###### heading", 6)]
    public void OnNewLineArrival_CreatesHeadingWithCorrectLevel(string line, int expectedLevel)
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();

        parser.OnMatch(line, ctx);

        Assert.AreEqual(1, ctx.Blocks.Count);
        var heading = ctx.Blocks[0] as HeadingBlock;
        Assert.IsNotNull(heading);
        Assert.AreEqual(expectedLevel, heading.Level);
    }

    [TestMethod]
    public void OnNewLineArrival_AddsBlockToContext()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();

        parser.OnMatch("# heading", ctx);

        Assert.AreEqual(1, ctx.Blocks.Count);
        Assert.IsInstanceOfType(ctx.Blocks[0], typeof(HeadingBlock));
    }

    [TestMethod]
    public void OnNewLineArrival_MultipleCalls_AddsMultipleBlocks()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();

        parser.OnMatch("# one", ctx);
        parser.OnMatch("## two", ctx);

        Assert.AreEqual(2, ctx.Blocks.Count);
        Assert.AreEqual(1, ((HeadingBlock)ctx.Blocks[0]).Level);
        Assert.AreEqual(2, ((HeadingBlock)ctx.Blocks[1]).Level);
    }

    // ═══════════════════════════════════════════
    //  Append — single call (line arrives whole)
    // ═══════════════════════════════════════════

    [DataTestMethod]
    [DataRow("# Heading 1\n", "Heading 1")]
    [DataRow("## Heading 2\n", "Heading 2")]
    [DataRow("### Heading 3\n", "Heading 3")]
    [DataRow("#### Heading 4\n", "Heading 4")]
    [DataRow("##### Heading 5\n", "Heading 5")]
    [DataRow("###### Heading 6\n", "Heading 6")]
    public void Append_StripsHashPrefix(string line, string expectedText)
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        parser.OnMatch(line, ctx);

        var completed = parser.Append(new TextChunk(line, true), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, completed);
        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual(expectedText, GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Append_ClosingHashes_AreStripped()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        var line = "## Heading ##\n";
        parser.OnMatch(line, ctx);

        var completed = parser.Append(new TextChunk(line, true), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, completed);
        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual("Heading", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Append_MultipleClosingHashes_AreAllStripped()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        var line = "## Heading ###\n";
        parser.OnMatch(line, ctx);

        parser.Append(new TextChunk(line, true), ctx);

        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual("Heading", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Append_EmptyContent_WithSpace_ProducesEmptyHeading()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        var line = "# \n";
        parser.OnMatch(line, ctx);

        var completed = parser.Append(new TextChunk(line, true), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, completed);
        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual("", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Append_OnlyHashAndNewline_ProducesEmptyHeading()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        var line = "#\n";
        parser.OnMatch(line, ctx);

        var completed = parser.Append(new TextChunk(line, true), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, completed);
        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual("", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Append_NoNewline_ReturnsFalse()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        var line = "# Hello";
        parser.OnMatch(line, ctx);

        var completed = parser.Append(new TextChunk(line, true), ctx);

        Assert.AreEqual(AppendResult.KeepFeeding, completed);
        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual("Hello", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Append_WithNewline_ReturnsTrue()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        parser.OnMatch("# Hello\n", ctx);

        var completed = parser.Append(new TextChunk("# Hello\n", true), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, completed);
    }

    [TestMethod]
    public void Append_ContentHasInlineAsterisks_PassesThrough()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        var line = "# Hello *World*\n";
        parser.OnMatch(line, ctx);

        parser.Append(new TextChunk(line, true), ctx);

        var heading = (HeadingBlock)ctx.Blocks[0];
        // 原始缓冲区保留完整内容（含 *）
        Assert.AreEqual("Hello *World*", heading.Inlines.RawBuffer.ToString());
    }

    [TestMethod]
    public void Append_NewlineOnly_ReturnsTrueAndNoContent()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        parser.OnMatch("# hi", ctx);

        // Prior Append without newline
        parser.Append(new TextChunk("# hi", true), ctx);

        // Newline-only chunk
        var completed = parser.Append(new TextChunk("\n", false), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, completed);
        // Content unchanged
        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual("hi", GetInlinesText(heading.Inlines));
    }

    // ═══════════════════════════════════════════
    //  Append — continuation (split chunks)
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Append_Continuation_AppendsText()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        parser.OnMatch("## Hel", ctx);

        // First call with same line (no newline)
        parser.Append(new TextChunk("## Hel", true), ctx);

        // Continuation
        var completed = parser.Append(new TextChunk("lo\n", false), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, completed);
        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual("Hello", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Append_ContinuationWithoutNewline_ReturnsFalse()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        parser.OnMatch("# He", ctx);
        parser.Append(new TextChunk("# He", true), ctx);

        var completed = parser.Append(new TextChunk("llo", false), ctx);

        Assert.AreEqual(AppendResult.KeepFeeding, completed);
    }

    [TestMethod]
    public void Append_ContinuationWithNewline_ReturnsTrue()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        parser.OnMatch("# He", ctx);
        parser.Append(new TextChunk("# He", true), ctx);

        var completed = parser.Append(new TextChunk("llo\n", false), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, completed);
        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual("Hello", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Append_MultipleContinuations()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        parser.OnMatch("## A", ctx);
        parser.Append(new TextChunk("## A", true), ctx);     // first = "A"
        parser.Append(new TextChunk("B", false), ctx);        // continuation
        parser.Append(new TextChunk("C", false), ctx);        // continuation

        var completed = parser.Append(new TextChunk("D\n", false), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, completed);
        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual("ABCD", GetInlinesText(heading.Inlines));
    }

    // ═══════════════════════════════════════════
    //  NeedNextChunk (ends with #)
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Append_ContinuationEndingWithHash_ReturnsNeedNextChunk()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        parser.OnMatch("# Hello", ctx);
        parser.Append(new TextChunk("# Hello", true), ctx);

        var result = parser.Append(new TextChunk("#", false), ctx);

        Assert.AreEqual(AppendResult.NeedNextChunk, result);
    }

    [TestMethod]
    public void Append_ContinuationEndingWithSpaceThenHash_ReturnsNeedNextChunk()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        parser.OnMatch("## Hello", ctx);
        parser.Append(new TextChunk("## Hello", true), ctx);

        var result = parser.Append(new TextChunk(" #", false), ctx);

        Assert.AreEqual(AppendResult.NeedNextChunk, result);
        var heading = (HeadingBlock)ctx.Blocks[0];
        Assert.AreEqual("Hello", GetInlinesText(heading.Inlines));
    }

    // ═══════════════════════════════════════════
    //  Feed integration (via StreamParser)
    // ═══════════════════════════════════════════

    [DataTestMethod]
    [DataRow("# Heading 1\n", 1, "Heading 1")]
    [DataRow("## Heading 2\n", 2, "Heading 2")]
    [DataRow("### Heading 3\n", 3, "Heading 3")]
    [DataRow("#### Heading 4\n", 4, "Heading 4")]
    [DataRow("##### Heading 5\n", 5, "Heading 5")]
    [DataRow("###### Heading 6\n", 6, "Heading 6")]
    public void Feed_SingleChunk_CreatesCorrectHeading(string input, int expectedLevel, string expectedText)
    {
        var parser = new StreamParser();

        parser.Feed(input);

        Assert.AreEqual(1, parser.Blocks.Count);
        var heading = (HeadingBlock)parser.Blocks[0];
        Assert.AreEqual(expectedLevel, heading.Level);
        Assert.AreEqual(expectedText, GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Feed_SplitChunk_AccumulatesHeading()
    {
        var parser = new StreamParser();

        parser.Feed("## Hel");

        Assert.AreEqual(1, parser.Blocks.Count, "Block created on FullMatch even before newline");
        var heading = (HeadingBlock)parser.Blocks[0];
        Assert.AreEqual(2, heading.Level);
        Assert.AreEqual("Hel", GetInlinesText(heading.Inlines));

        parser.Feed("lo wo\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        Assert.AreEqual("Hello wo", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Feed_SplitChunk_ThreeParts()
    {
        var parser = new StreamParser();

        parser.Feed("## ");
        parser.Feed("Hel");
        parser.Feed("lo\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var heading = (HeadingBlock)parser.Blocks[0];
        Assert.AreEqual(2, heading.Level);
        Assert.AreEqual("Hello", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Feed_TwoHeadings_ProducesTwoBlocks()
    {
        var parser = new StreamParser();

        parser.Feed("# First\n## Second\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.AreEqual(1, ((HeadingBlock)parser.Blocks[0]).Level);
        Assert.AreEqual("First", GetInlinesText(((HeadingBlock)parser.Blocks[0]).Inlines));
        Assert.AreEqual(2, ((HeadingBlock)parser.Blocks[1]).Level);
        Assert.AreEqual("Second", GetInlinesText(((HeadingBlock)parser.Blocks[1]).Inlines));
    }

    [TestMethod]
    public void Feed_NoSpaceAfterHash_ProducesParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("##notheading\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Feed_ClosingHashes_Stripped()
    {
        var parser = new StreamParser();

        parser.Feed("## Heading ##\n");

        var heading = (HeadingBlock)parser.Blocks[0];
        Assert.AreEqual("Heading", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Feed_TooManyHashes_ProducesParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("####### seven hashes\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Feed_EmptyHeading_WithSpace()
    {
        var parser = new StreamParser();

        parser.Feed("# \n");

        var heading = (HeadingBlock)parser.Blocks[0];
        Assert.AreEqual("", GetInlinesText(heading.Inlines));
    }

    [TestMethod]
    public void Feed_HeadingTerminatesPreviousParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("some paragraph text\n");
        parser.Feed("# Heading\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ParagraphBlock));
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(HeadingBlock));
        Assert.AreEqual("Heading", GetInlinesText(((HeadingBlock)parser.Blocks[1]).Inlines));
    }

    [TestMethod]
    public void Feed_HeadingInMiddle_ProducesThreeBlocks()
    {
        var parser = new StreamParser();

        parser.Feed("para1\n# h\npara2\n");

        Assert.AreEqual(3, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ParagraphBlock));
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(HeadingBlock));
        Assert.IsInstanceOfType(parser.Blocks[2], typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Feed_EmptyChunk_NoBlocks()
    {
        var parser = new StreamParser();

        parser.Feed("");

        Assert.AreEqual(0, parser.Blocks.Count);
    }

    // ═══════════════════════════════════════════
    //  Edge cases
    // ═══════════════════════════════════════════

    [TestMethod]
    public void TryMatch_PreviousBlockIgnored()
    {
        // ATX heading TryMatch does not depend on previous block
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        ctx.Blocks.Add(new ParagraphBlock());

        var result = parser.TryMatch(new TextChunk("# heading", false), ctx);

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [TestMethod]
    public void Append_WhenPreviousBlockIsNotHeading_ReturnsTrue()
    {
        var parser = new AtxHeaderParser();
        var ctx = new ParserContext();
        ctx.Blocks.Add(new ParagraphBlock()); // wrong block type

        var result = parser.Append(new TextChunk("# text\n", true), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, result, "Should return NeedMatch to relinquish control when block type mismatch");
    }
}
