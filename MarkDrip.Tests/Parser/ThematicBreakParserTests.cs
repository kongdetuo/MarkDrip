using MarkDrip.Model;
using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class ThematicBreakParserTests
{
    // ═══════════════════════════════════════════
    //  TryMatch
    // ═══════════════════════════════════════════

    [DataTestMethod]
    [DataRow("---\n")]
    [DataRow("***\n")]
    [DataRow("___\n")]
    [DataRow("----\n")]
    [DataRow("---------\n")]
    [DataRow(" - --\n")]
    [DataRow("  ---\n")]
    [DataRow("   ---\n")]
    [DataRow("- - -\n")]
    [DataRow("* * *\n")]
    [DataRow("** ** **\n")]
    [DataRow("--- \n")]
    [DataRow("---\t\n")]
    public void TryMatch_ThematicBreak_ReturnsFullMatch(string line)
    {
        var parser = new ThematicBreakParser();

        var result = parser.TryMatch(new TextChunk(line, false), new ParserContext());

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [DataTestMethod]
    [DataRow("--\n")]           // too few
    [DataRow("-*-\n")]          // mixed chars
    [DataRow("---x\n")]         // trailing text
    [DataRow("    ---\n")]      // 4 spaces indent
    [DataRow("abc\n")]          // not a marker
    [DataRow("")]               // empty
    [DataRow("\n")]             // blank
    public void TryMatch_NotThematicBreak_ReturnsNoMatch(string line)
    {
        var parser = new ThematicBreakParser();

        var result = parser.TryMatch(new TextChunk(line, false), new ParserContext());

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    [TestMethod]
    public void TryMatch_DashAfterOpenParagraph_ReturnsNoMatch()
    {
        // --- after an open paragraph should be a setext heading, not thematic break
        var parser = new ThematicBreakParser();
        var context = new ParserContext();
        context.Blocks.Add(new ParagraphBlock { Status = BlockStatus.Open });

        var result = parser.TryMatch(new TextChunk("---\n", false), context);

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    [TestMethod]
    public void TryMatch_StarAfterOpenParagraph_ReturnsFullMatch()
    {
        // *** after an open paragraph is still a thematic break (cannot be a setext heading)
        var parser = new ThematicBreakParser();
        var context = new ParserContext();
        context.Blocks.Add(new ParagraphBlock { Status = BlockStatus.Open });

        var result = parser.TryMatch(new TextChunk("***\n", false), context);

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [TestMethod]
    public void TryMatch_DashAfterFinalizedParagraph_ReturnsFullMatch()
    {
        // --- after a finalized paragraph should be a thematic break
        var parser = new ThematicBreakParser();
        var context = new ParserContext();
        context.Blocks.Add(new ParagraphBlock { Status = BlockStatus.Finalized });

        var result = parser.TryMatch(new TextChunk("---\n", false), context);

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    // ═══════════════════════════════════════════
    //  OnMatch / Append
    // ═══════════════════════════════════════════

    [TestMethod]
    public void OnMatch_CreatesThematicBreakBlock()
    {
        var parser = new ThematicBreakParser();
        var context = new ParserContext();

        parser.OnMatch("---\n", context);

        Assert.AreEqual(1, context.Blocks.Count);
        Assert.IsInstanceOfType(context.Blocks[0], typeof(ThematicBreakBlock));
        Assert.AreEqual(BlockStatus.Finalized, context.Blocks[0].Status);
    }

    [TestMethod]
    public void Append_ReturnsNeedMatch()
    {
        var parser = new ThematicBreakParser();

        var result = parser.Append(new TextChunk("anything\n", false), new ParserContext());

        Assert.AreEqual(AppendResult.NextLineNeedMatch, result);
    }

    // ═══════════════════════════════════════════
    //  Integration via StreamParser
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_ThematicBreak_Dashed()
    {
        var parser = new StreamParser();

        parser.Feed("---\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ThematicBreakBlock));
    }

    [TestMethod]
    public void Feed_ThematicBreak_Stars()
    {
        var parser = new StreamParser();

        parser.Feed("***\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ThematicBreakBlock));
    }

    [TestMethod]
    public void Feed_ThematicBreak_Underscores()
    {
        var parser = new StreamParser();

        parser.Feed("___\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ThematicBreakBlock));
    }

    [TestMethod]
    public void Feed_TwoMinusNotThematicBreak()
    {
        var parser = new StreamParser();

        parser.Feed("--\n");

        // -- with just 2 chars should be a paragraph
        Assert.AreEqual(1, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Complete_FinalizesThematicBreak()
    {
        var parser = new StreamParser();

        parser.Feed("---\n");
        parser.Complete();

        // ThematicBreakBlock is already Finalized on construction
        Assert.AreEqual(BlockStatus.Finalized, parser.Blocks[0].Status);
    }
}
