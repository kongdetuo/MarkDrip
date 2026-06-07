using MarkDrip.Model;
using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class BlockQuoteParserTests
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

    // ═══════════════════════════════════════════════
    //  TryMatch
    // ═══════════════════════════════════════════════

    [DataTestMethod]
    [DataRow(">")]
    [DataRow("> ")]
    [DataRow(">text")]
    [DataRow(" >")]
    [DataRow("  >")]
    [DataRow("   >")]
    [DataRow("  > text")]
    [DataRow("   > text")]
    public void TryMatch_StartsWithGtAtLineStart_ReturnsFullMatch(string line)
    {
        var parser = new BlockQuoteParser();
        var result = parser.TryMatch(new TextChunk(line, true, false), new ParserContext());
        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [DataTestMethod]
    [DataRow("text")]
    [DataRow("    >")]
    [DataRow("    > text")]
    [DataRow("\n")]
    [DataRow(" \n")]
    public void TryMatch_NoGtAtLineStart_ReturnsNoMatch(string line)
    {
        var parser = new BlockQuoteParser();
        var isLineEnd = line.Contains('\n') || line.Contains('\r');
        var result = parser.TryMatch(new TextChunk(line, true, isLineEnd), new ParserContext());
        Assert.AreEqual(MatchResult.NoMatch, result);
    }



    [DataTestMethod]
    [DataRow(" ")]
    [DataRow("  ")]
    [DataRow("   ")]
    public void TryMatch_BlankAtLineStartNotLineEnd_ReturnsPartialMatch(string line)
    {
        var parser = new BlockQuoteParser();
        var result = parser.TryMatch(new TextChunk(line, true, false), new ParserContext());
        Assert.AreEqual(MatchResult.PartialMatch, result);
    }

    // ═══════════════════════════════════════════════
    //  OnMatch
    // ═══════════════════════════════════════════════

    [TestMethod]
    public void OnMatch_CreatesBlockQuoteBlock()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();

        parser.OnMatch("> text", ctx);

        Assert.AreEqual(1, ctx.Blocks.Count);
        Assert.IsInstanceOfType(ctx.Blocks[0], typeof(BlockQuoteBlock));
    }

    [TestMethod]
    public void OnMatch_ConsecutiveCallWithOpenBlock_ReusesExistingBlock()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();

        parser.OnMatch("> first", ctx);
        parser.OnMatch("> second", ctx);

        Assert.AreEqual(1, ctx.Blocks.Count, "Should reuse same BlockQuote");
    }

    // ═══════════════════════════════════════════════
    //  Append — direct, whole-line chunks
    // ═══════════════════════════════════════════════

    [TestMethod]
    public void Append_BeforeOnMatch_ReturnsNextLineNeedMatch()
    {
        var parser = new BlockQuoteParser();
        var result = parser.Append(new TextChunk("> text\n", true, true), new ParserContext());
        Assert.AreEqual(AppendResult.NextLineNeedMatch, result);
    }

    [TestMethod]
    public void Append_ContinuationNotAtLineStart_FeedsDirectly()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> text\n", ctx);

        var result = parser.Append(new TextChunk(" more\n", false, true), ctx);

        Assert.AreEqual(AppendResult.KeepFeeding, result);
    }

    [TestMethod]
    public void Append_BlankAtLineStartNotLineEnd_ReturnsNeedNextChunk()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> text\n", ctx);

        var result = parser.Append(new TextChunk("  ", true, false), ctx);

        Assert.AreEqual(AppendResult.NeedNextChunk, result);
    }

    [TestMethod]
    public void Append_GtLine_StripsPrefix_FeedsContent()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> A wise quote.\n", ctx);

        var result = parser.Append(new TextChunk("> A wise quote.\n", true, true), ctx);

        Assert.AreEqual(AppendResult.KeepFeeding, result);
        var quote = (BlockQuoteBlock)ctx.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count);
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("A wise quote.", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Append_GtWithLeadingSpaces_StripsPrefix_FeedsContent()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("  > text\n", ctx);

        parser.Append(new TextChunk("  > text\n", true, true), ctx);

        var quote = (BlockQuoteBlock)ctx.Blocks[0];
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("text", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Append_GtLineWithoutContentAfterStrip_FeedsNewline()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> text\n", ctx);
        parser.Append(new TextChunk("> text\n", true, true), ctx);

        var result = parser.Append(new TextChunk(">\n", true, true), ctx);

        Assert.AreEqual(AppendResult.KeepFeeding, result);
    }

    [TestMethod]
    public void Append_GtWithoutNewline_ReturnsNeedNextChunk()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> text\n", ctx);

        var result = parser.Append(new TextChunk(">", true, false), ctx);

        Assert.AreEqual(AppendResult.NeedNextChunk, result);
    }

    [TestMethod]
    public void Append_BlankLine_EndsQuote()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> text\n", ctx);
        parser.Append(new TextChunk("> text\n", true, true), ctx);

        var result = parser.Append(new TextChunk("\n", true, true), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, result);
    }

    [TestMethod]
    public void Append_LazyContinuation_FeedsDirectly()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> quote\n", ctx);
        parser.Append(new TextChunk("> quote\n", true, true), ctx);

        var result = parser.Append(new TextChunk("lazy continuation\n", true, true), ctx);

        Assert.AreEqual(AppendResult.KeepFeeding, result);
        var quote = (BlockQuoteBlock)ctx.Blocks[0];
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("quote\nlazy continuation", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Append_MultipleGtLines_SameParagraph()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> first\n", ctx);
        parser.Append(new TextChunk("> first\n", true, true), ctx);
        parser.Append(new TextChunk("> second\n", true, true), ctx);

        var quote = (BlockQuoteBlock)ctx.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count);
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("first\nsecond", GetInlinesText(para.Inlines));
    }

    // ═══════════════════════════════════════════════
    //  Complete
    // ═══════════════════════════════════════════════

    [TestMethod]
    public void Complete_WhenNotInitialized_DoesNotThrow()
    {
        var parser = new BlockQuoteParser();
        parser.Complete(new ParserContext());
    }

    [TestMethod]
    public void Complete_AfterOnMatch_DoesNotThrow()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> text\n", ctx);
        parser.Complete(ctx);
    }

    // ═══════════════════════════════════════════════
    //  Feed — integration via StreamParser
    // ═══════════════════════════════════════════════

    [TestMethod]
    public void Feed_SingleLine_CreatesBlockQuote()
    {
        var parser = new StreamParser();

        parser.Feed("> A wise quote.\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var quote = (BlockQuoteBlock)parser.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count);
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("A wise quote.", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Feed_MultiLine_SameParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("> First line\n> Second line\n");

        var quote = (BlockQuoteBlock)parser.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count);
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("First line\nSecond line", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Feed_BlankLineWithPrefix_SeparatesParagraphs()
    {
        var parser = new StreamParser();

        parser.Feed("> Para one\n>\n> Para two\n");

        var quote = (BlockQuoteBlock)parser.Blocks[0];
        Assert.AreEqual(2, quote.Children.Count);
        Assert.AreEqual("Para one", GetInlinesText(((ParagraphBlock)quote.Children[0]).Inlines));
        Assert.AreEqual("Para two", GetInlinesText(((ParagraphBlock)quote.Children[1]).Inlines));
    }

    [TestMethod]
    public void Feed_LazyContinuation_AppendsToParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("> Quote\nlazy continuation\n");

        var quote = (BlockQuoteBlock)parser.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count);
        Assert.AreEqual("Quote\nlazy continuation", GetInlinesText(((ParagraphBlock)quote.Children[0]).Inlines));
    }

    [TestMethod]
    public void Feed_EndedByBlankLine_StartsNewOuterBlock()
    {
        var parser = new StreamParser();

        parser.Feed("> Quote content\n\nNormal paragraph\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(BlockQuoteBlock));
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(ParagraphBlock));
        Assert.AreEqual("Quote content", GetInlinesText(((ParagraphBlock)((BlockQuoteBlock)parser.Blocks[0]).Children[0]).Inlines));
        Assert.AreEqual("Normal paragraph", GetInlinesText(((ParagraphBlock)parser.Blocks[1]).Inlines));
    }

    [TestMethod]
    public void Feed_NestedQuote_CreatesNestedBlockQuote()
    {
        var parser = new StreamParser();

        parser.Feed("> > nested\n");

        var outerQuote = (BlockQuoteBlock)parser.Blocks[0];
        Assert.AreEqual(1, outerQuote.Children.Count);
        var innerQuote = outerQuote.Children[0] as BlockQuoteBlock;
        Assert.IsNotNull(innerQuote, "Should have a nested BlockQuote");
        Assert.AreEqual(1, innerQuote.Children.Count);
        var para = (ParagraphBlock)innerQuote.Children[0];
        Assert.AreEqual("nested", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Feed_EmptyChunk_NoBlocks()
    {
        var parser = new StreamParser();

        parser.Feed("");

        Assert.AreEqual(0, parser.Blocks.Count);
    }

    [TestMethod]
    public void Feed_EmptyQuoteLine_WithContentAfter()
    {
        var parser = new StreamParser();

        parser.Feed(">\n> content\n");

        var quote = (BlockQuoteBlock)parser.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count);
        Assert.AreEqual("content", GetInlinesText(((ParagraphBlock)quote.Children[0]).Inlines));
    }

    // ═══════════════════════════════════════════════
    //  Feed — streaming (char-by-char)
    // ═══════════════════════════════════════════════

    [TestMethod]
    public void Feed_Streaming_QuoteThenParagraph()
    {
        var parser = new StreamParser();

        foreach (char c in "> A quote.\n\nNormal.\n")
            parser.Feed(c.ToString());

        Assert.AreEqual(2, parser.Blocks.Count);
        var quote = (BlockQuoteBlock)parser.Blocks[0];
        var para = (ParagraphBlock)parser.Blocks[1];
        Assert.AreEqual("A quote.", GetInlinesText(((ParagraphBlock)quote.Children[0]).Inlines));
        Assert.AreEqual("Normal.", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Feed_Streaming_QuoteContentNotOutside()
    {
        var parser = new StreamParser();

        string content = "> *混合样式*：在引用块内使用 **粗体** 和 `代码`。\n";
        foreach (char c in content)
            parser.Feed(c.ToString());

        Assert.AreEqual(1, parser.Blocks.Count, "Only one block (the quote) expected");
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(BlockQuoteBlock));
        var quote = (BlockQuoteBlock)parser.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count, "One paragraph inside quote");
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("*混合样式*：在引用块内使用 **粗体** 和 `代码`。",
            para.Inlines.RawBuffer.ToString());
    }
}
