using MarkDrip.Model;
using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class IndentedCodeBlockParserTests
{
    // ═══════════════════════════════════════════
    //  TryMatch
    // ═══════════════════════════════════════════

    [DataTestMethod]
    [DataRow("    code\n")]
    [DataRow("    console.log('hello')\n")]
    [DataRow("        deeply indented\n")]      // 8 spaces
    [DataRow("     6 spaces\n")]                // 6 spaces
    [DataRow("    # not a heading\n")]           // 4 spaces + ATX marker = code
    [DataRow("    - not a list\n")]             // 4 spaces + list marker = code
    [DataRow("    > not a quote\n")]            // 4 spaces + quote marker = code
    public void TryMatch_4OrMoreSpacesWithContent_ReturnsFullMatch(string line)
    {
        var parser = new IndentedCodeBlockParser();

        var result = parser.TryMatch(new TextChunk(line, false, line.Contains('\n')), new ParserContext());

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [DataTestMethod]
    [DataRow("   text\n")]      // 3 spaces
    [DataRow("  text\n")]       // 2 spaces
    [DataRow(" text\n")]        // 1 space
    [DataRow("text\n")]         // 0 spaces
    [DataRow("\n")]             // blank
    [DataRow("    \n")]         // 4 spaces but blank line
    [DataRow("     \n")]        // 5 spaces but blank line
    public void TryMatch_NotIndentedCode_ReturnsNoMatch(string line)
    {
        var parser = new IndentedCodeBlockParser();

        var result = parser.TryMatch(new TextChunk(line, false, line.Contains('\n')), new ParserContext());

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    [TestMethod]
    public void TryMatch_EmptyString_ReturnsPartialMatch()
    {
        var parser = new IndentedCodeBlockParser();

        var result = parser.TryMatch(new TextChunk("", false, false), new ParserContext());

        Assert.AreEqual(MatchResult.PartialMatch, result);
    }

    [TestMethod]
    public void TryMatch_AfterOpenParagraph_ReturnsNoMatch()
    {
        // Indented code cannot interrupt a paragraph (CommonMark 4.4)
        var parser = new IndentedCodeBlockParser();
        var context = new ParserContext();
        context.Blocks.Add(new ParagraphBlock { Status = BlockStatus.Open });

        var result = parser.TryMatch(new TextChunk("    code\n", false, true), context);

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    // ═══════════════════════════════════════════
    //  OnMatch
    // ═══════════════════════════════════════════

    [TestMethod]
    public void OnMatch_CreatesCodeBlock()
    {
        var parser = new IndentedCodeBlockParser();
        var ctx = new ParserContext();

        parser.OnMatch("    code\n", ctx);

        Assert.AreEqual(1, ctx.Blocks.Count);
        Assert.IsInstanceOfType(ctx.Blocks[0], typeof(CodeBlock));
    }

    [TestMethod]
    public void OnMatch_DoesNotProcessContent()
    {
        var parser = new IndentedCodeBlockParser();
        var ctx = new ParserContext();

        parser.OnMatch("    hello\n", ctx);
        parser.Append(new TextChunk("    hello\n", true, true), ctx);
        parser.Complete(ctx);
        var code = (CodeBlock)ctx.Blocks[0];
        Assert.AreEqual("hello", code.Content.ToString());
    }

    // ═══════════════════════════════════════════
    //  Feed integration
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_IndentedCode_SingleLine()
    {
        var parser = new StreamParser();

        parser.Feed("    hello\n");
        parser.Complete();

        Assert.AreEqual(1, parser.Blocks.Count);
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("hello", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_MultiLine()
    {
        var parser = new StreamParser();

        parser.Feed("    line1\n    line2\n    line3\n");
        parser.Complete();
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("line1\nline2\nline3", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_ExtraIndentPreserved()
    {
        // 8 spaces → only 4 stripped, remaining 4 spaces in content
        var parser = new StreamParser();

        parser.Feed("        extra indent\n");
        parser.Complete();
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("    extra indent", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_BlankLineBetweenChunks_Preserved()
    {
        var parser = new StreamParser();

        parser.Feed("    line1\n\n    line2\n");
        parser.Complete();

        var code = (CodeBlock)parser.Blocks[0];
        // blank line between chunks is part of content
        Assert.AreEqual("line1\n\nline2", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_MultipleBlankLinesBetweenChunks_Preserved()
    {
        var parser = new StreamParser();

        parser.Feed("    a\n\n\n    b\n");
        parser.Complete();
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("a\n\n\nb", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_TrailingBlankLineNotIncluded()
    {
        // blank line followed by non-indented → blank is NOT part of content
        var parser = new StreamParser();

        parser.Feed("    code\n\nnot code\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("code", code.Content.ToString());
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Feed_IndentedCode_FollowedByParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("    code\n\nparagraph text\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(CodeBlock));
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(ParagraphBlock));
        Assert.AreEqual("code", ((CodeBlock)parser.Blocks[0]).Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_DoesNotInterruptParagraph()
    {
        // An indented code block cannot interrupt a paragraph (CommonMark 4.4).
        // It becomes continuation of the paragraph (lazy continuation) or ends it.
        var parser = new StreamParser();

        parser.Feed("paragraph start\n    indented\n");

        // The "    indented" should NOT start an indented code block
        // It should be part of the paragraph
        Assert.AreEqual(1, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Feed_IndentedCode_NotInterruptedByListMarker()
    {
        // "    - text\n" is indented code, not a list
        var parser = new StreamParser();

        parser.Feed("    - list?\n");
        parser.Complete();

        Assert.AreEqual(1, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(CodeBlock));
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("- list?", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_AtEndOfFile_StaysAsBlock()
    {
        var parser = new StreamParser();

        parser.Feed("    code\n");
        parser.Complete();

        Assert.AreEqual(1, parser.Blocks.Count);
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("code", code.Content.ToString());
        Assert.AreEqual(BlockStatus.Finalized, code.Status);
    }

    [TestMethod]
    public void Feed_IndentedCode_UnclosedAtEOF_Finalized()
    {
        var parser = new StreamParser();

        parser.Feed("    line1\n    line2\n");
        parser.Complete();

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("line1\nline2", code.Content.ToString());
        Assert.AreEqual(BlockStatus.Finalized, code.Status);
    }

    [TestMethod]
    public void Feed_IndentedCode_WithFenceInside()
    {
        // "    ```\n" is indented code (content: "```"), not a fenced code block
        var parser = new StreamParser();

        parser.Feed("    ```\n    code inside\n    ```\n");
        parser.Complete();

        Assert.AreEqual(1, parser.Blocks.Count);
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("```\ncode inside\n```", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_ThemeBreakInside_IsCode()
    {
        // "    ---\n" inside indented code is content, not a thematic break
        var parser = new StreamParser();

        parser.Feed("    text\n    ---\n    more\n");
        parser.Complete();

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("text\n---\nmore", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_ThenFencedCode()
    {
        // indented code followed by fenced code should produce two blocks
        var parser = new StreamParser();

        parser.Feed("    indented\n\n```\nfenced\n```\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(CodeBlock));
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(CodeBlock));
        Assert.AreEqual("indented", ((CodeBlock)parser.Blocks[0]).Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_EmptyLineBetweenChunksNotAtEnd()
    {
        // Two blank lines between chunks should be fully preserved
        var parser = new StreamParser();

        parser.Feed("    chunk1\n\n\n    chunk2\n    chunk3\n");
        parser.Complete();

        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("chunk1\n\n\nchunk2\nchunk3", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_SingleWord_NoNewline()
    {
        // No trailing newline in the input - should still work
        var parser = new StreamParser();

        parser.Feed("    code");

        Assert.AreEqual(1, parser.Blocks.Count);
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("code", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_ThenParagraphNoBlankLine()
    {
        // Indented code followed immediately by non-indented text (no blank line)
        // The non-indented text ends the code block
        var parser = new StreamParser();

        parser.Feed("    code\nnot indented\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("code", code.Content.ToString());
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Feed_IndentedCode_BlockQuoteInside()
    {
        // "    > quote\n" is indented code, not a block quote
        var parser = new StreamParser();

        parser.Feed("    > quote\n");
        parser.Complete();

        Assert.AreEqual(1, parser.Blocks.Count);
        var code = (CodeBlock)parser.Blocks[0];
        Assert.AreEqual("> quote", code.Content.ToString());
    }

    [TestMethod]
    public void Feed_IndentedCode_PreservesBlankLineInMiddleOnlyWhenFollowed()
    {
        // Blank line should be preserved only if followed by more indented content
        var parser = new StreamParser();

        parser.Feed("    code\n\n    more\n\n\n    last\n");
        parser.Complete();

        var code = (CodeBlock)parser.Blocks[0];
        // First blank: between "code" and "more" → preserved
        // Second blank group (2 blanks): between "more" and "last" → preserved
        Assert.AreEqual("code\n\nmore\n\n\nlast", code.Content.ToString());
    }
}
