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

    // ═══════════════════════════════════════════
    //  TryMatch
    // ═══════════════════════════════════════════

    [DataTestMethod]
    [DataRow(">")]
    [DataRow("> ")]
    [DataRow(">text")]
    [DataRow(" >")]
    [DataRow("  >")]
    [DataRow("   >")]
    [DataRow("  > text")]
    [DataRow("   > text")]
    public void TryMatch_StartsWithGreaterThan_ReturnsFullMatch(string line)
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();

        var result = parser.TryMatch(new TextChunk(line, false, false), ctx);

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("text")]
    [DataRow("    >")] // 4+ spaces before > → not a block quote
    [DataRow("    > text")]
    public void TryMatch_NoGreaterThan_ReturnsNoMatch(string line)
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();

        var result = parser.TryMatch(new TextChunk(line, false, false), ctx);

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    // ═══════════════════════════════════════════
    //  OnMatch
    // ═══════════════════════════════════════════

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
    public void OnMatch_ConsecutiveCall_ReusesExistingBlockQuote()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();

        parser.OnMatch("> first", ctx);
        parser.OnMatch("> second", ctx);

        Assert.AreEqual(1, ctx.Blocks.Count, "Should reuse same BlockQuote");
    }

    // ═══════════════════════════════════════════
    //  Append — prefix stripping & forwarding
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Append_StripsPrefix_AndCreatesInnerParagraph()
    {
        var parser = new BlockQuoteParser();
        var buffer = new LineBuffer();
        buffer.Append("> text\n");
        var ctx = new ParserContext(new LineBufferView(buffer));
        parser.OnMatch("> text\n", ctx);

        var result = parser.Append(new TextChunk("> text\n", false, false), ctx);

        Assert.AreEqual(AppendResult.KeepFeeding, result);
        var quote = (BlockQuoteBlock)ctx.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count);
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("text", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Append_WithLeadingSpace_PrefixStripped()
    {
        var parser = new BlockQuoteParser();
        var buffer = new LineBuffer();
        buffer.Append("  > text\n");
        var ctx = new ParserContext(new LineBufferView(buffer));
        parser.OnMatch("  > text\n", ctx);

        parser.Append(new TextChunk("  > text\n", false, false), ctx);

        var quote = (BlockQuoteBlock)ctx.Blocks[0];
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("text", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Append_LazyContinuation_ForwardsAsIs()
    {
        var parser = new BlockQuoteParser();
        var buffer = new LineBuffer();
        var ctx = new ParserContext(new LineBufferView(buffer));
        parser.OnMatch("> quote\n", ctx);

        buffer.Append("> quote\n");
        parser.Append(new TextChunk("> quote\n", false, false), ctx);
        buffer.Clear(); // 模拟 StreamParser 的行完结清理

        buffer.Append("lazy continuation\n");
        var result = parser.Append(new TextChunk("lazy continuation\n", false, false), ctx);

        Assert.AreEqual(AppendResult.KeepFeeding, result);
        var quote = (BlockQuoteBlock)ctx.Blocks[0];
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("quote\nlazy continuation", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Append_BlankLineWithoutPrefix_EndsQuote()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> text\n", ctx);
        parser.Append(new TextChunk("> text\n", false, false), ctx);

        var result = parser.Append(new TextChunk("\n", false, false), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, result, "Blank line without > should end quote");
    }

    [TestMethod]
    public void Append_EmptyPrefixLine_StaysInQuote()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> text\n", ctx);
        parser.Append(new TextChunk("> text\n", false, false), ctx);

        // >\n (empty prefix line) stays in quote
        var result = parser.Append(new TextChunk(">\n", false, false), ctx);

        Assert.AreEqual(AppendResult.KeepFeeding, result);
    }

    // ═══════════════════════════════════════════
    //  Feed integration (via StreamParser)
    // ═══════════════════════════════════════════

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
    public void Feed_WithHeading_CreatesHeadingAndParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("> # Title\n> Text\n");

        var quote = (BlockQuoteBlock)parser.Blocks[0];
        Assert.AreEqual(2, quote.Children.Count);
        Assert.IsInstanceOfType(quote.Children[0], typeof(HeadingBlock));
        Assert.AreEqual(1, ((HeadingBlock)quote.Children[0]).Level);
        Assert.AreEqual("Title", GetInlinesText(((HeadingBlock)quote.Children[0]).Inlines));
        Assert.IsInstanceOfType(quote.Children[1], typeof(ParagraphBlock));
        Assert.AreEqual("Text", GetInlinesText(((ParagraphBlock)quote.Children[1]).Inlines));
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

    // ═══════════════════════════════════════════
    //  Streaming (character-by-character) tests
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Append_Streaming_QuotePrefixAndContentSeparate_Works()
    {
        // "> text\n" arriving character by character
        var parser = new BlockQuoteParser();
        var buffer = new LineBuffer();
        var ctx = new ParserContext(new LineBufferView(buffer));
        parser.OnMatch("> \n", ctx);  // simulates first TryMatch+OnMatch

        // Each char in "> text\n" arrives independently, simulating StreamParser.Feed dispatch
        // 用 result 捕获最后一次 Append（"\n"）的返回值
        AppendResult result = AppendResult.KeepFeeding;
        foreach (char c in "> text\n")
        {
            buffer.Append(c.ToString());
            result = parser.Append(new TextChunk(c.ToString(), false, false), ctx);
        }

        Assert.AreEqual(AppendResult.KeepFeeding, result);
        var quote = (BlockQuoteBlock)ctx.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count);
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("text", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Append_Streaming_BlankLineAfterQuote_EndsQuote()
    {
        var parser = new BlockQuoteParser();
        var ctx = new ParserContext();
        parser.OnMatch("> \n", ctx);

        // Stream "> hello\n" character by character
        foreach (char c in "> hello\n")
        {
            parser.Append(new TextChunk(c.ToString(), false, false), ctx);
        }

        // Then blank line
        var result = parser.Append(new TextChunk("\n", false, false), ctx);

        Assert.AreEqual(AppendResult.NextLineNeedMatch, result, "Blank line should end the quote");
    }

    [TestMethod]
    public void Append_Streaming_MultipleQuoteLines_SameParagraph()
    {
        // Consecutive > lines without blank > separator → single paragraph with soft breaks
        var parser = new BlockQuoteParser();
        var buffer = new LineBuffer();
        var ctx = new ParserContext(new LineBufferView(buffer));
        parser.OnMatch("> \n", ctx);

        foreach (char c in "> first\n")
        {
            buffer.Append(c.ToString());
            parser.Append(new TextChunk(c.ToString(), false, false), ctx);
        }
        buffer.Clear(); // 行完结清理

        foreach (char c in "> second\n")
        {
            buffer.Append(c.ToString());
            parser.Append(new TextChunk(c.ToString(), false, false), ctx);
        }

        var quote = (BlockQuoteBlock)ctx.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count, "Consecutive > lines → one paragraph");
        var para = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual("first\nsecond", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Feed_Streaming_QuoteThenParagraph()
    {
        // Full streaming scenario: quote followed by normal paragraph
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
        // The exact scenario from the bug report:
        // "> *混合样式*：在引用块内使用 **粗体** 和 `代码`。" streamed char-by-char
        var parser = new StreamParser();

        string content = "> *混合样式*：在引用块内使用 **粗体** 和 `代码`。\n";
        foreach (char c in content)
            parser.Feed(c.ToString());

        // Content should be INSIDE the quote, not outside
        Assert.AreEqual(1, parser.Blocks.Count, "Only one block (the quote) expected");
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(BlockQuoteBlock));
        var quote = (BlockQuoteBlock)parser.Blocks[0];
        Assert.AreEqual(1, quote.Children.Count, "One paragraph inside quote");
        var para = (ParagraphBlock)quote.Children[0];
        // Use RawBuffer to compare full content including inline markup
        Assert.AreEqual("*混合样式*：在引用块内使用 **粗体** 和 `代码`。",
            para.Inlines.RawBuffer.ToString());
    }
}
