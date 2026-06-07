using MarkDrip.Model;
using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class ListParserTests
{
    // ═══════════════════════════════════════════
    //  TryMatch
    // ═══════════════════════════════════════════

    [DataTestMethod]
    [DataRow("- item\n")]
    [DataRow("* item\n")]
    [DataRow("+ item\n")]
    [DataRow(" - item\n")]
    [DataRow("  - item\n")]
    [DataRow("   - item\n")]
    [DataRow("1. item\n")]
    [DataRow("123. item\n")]
    [DataRow("1) item\n")]
    [DataRow("1.\n")]
    [DataRow("-\n")]
    [DataRow("- \n")]
    public void TryMatch_ListMarker_ReturnsFullMatch(string line)
    {
        var parser = new ListParser();

        var result = parser.TryMatch(new TextChunk(line, false), new ParserContext());

        Assert.AreEqual(MatchResult.FullMatch, result);
    }

    [DataTestMethod]
    [DataRow("    - item\n")]   // 4 spaces → indented code, not list
    [DataRow("-item\n")]        // no space after marker
    [DataRow("1.item\n")]       // no space after delimiter
    [DataRow("abc\n")]          // not a marker
    [DataRow("> - item\n")]     // blockquote, not list
    [DataRow("```\n")]          // code fence
    [DataRow("")]               // empty
    public void TryMatch_NotMarker_ReturnsNoMatch(string line)
    {
        var parser = new ListParser();

        var result = parser.TryMatch(new TextChunk(line, false), new ParserContext());

        Assert.AreEqual(MatchResult.NoMatch, result);
    }

    // ═══════════════════════════════════════════
    //  Basic lists
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_UnorderedList_TwoItems()
    {
        var parser = new StreamParser();

        parser.Feed("- item1\n- item2\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(ListStyle.Bullet, list.Style);
        Assert.AreEqual(2, list.Items.Count);
        Assert.IsFalse(list.IsLoose);
    }

    [TestMethod]
    public void Feed_OrderedList_TwoItems()
    {
        var parser = new StreamParser();

        parser.Feed("1. first\n2. second\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(ListStyle.Ordered, list.Style);
        Assert.AreEqual(2, list.Items.Count);
    }

    [TestMethod]
    public void Feed_OrderedListWithParenDelimiter()
    {
        var parser = new StreamParser();

        parser.Feed("1) first\n2) second\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(ListStyle.Ordered, list.Style);
        Assert.AreEqual(2, list.Items.Count);
    }

    [TestMethod]
    public void Feed_ListWithContinuation()
    {
        var parser = new StreamParser();

        parser.Feed("- item\n  continuation\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(1, list.Items.Count);
        Assert.AreEqual("item\ncontinuation", GetItemText(list.Items[0]));
    }

    // ═══════════════════════════════════════════
    //  Blank line behavior
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_LooseList_OneBlankLineBetweenItems()
    {
        var parser = new StreamParser();

        parser.Feed("- item1\n\n- item2\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var list = (ListBlock)parser.Blocks[0];
        Assert.IsTrue(list.IsLoose);
        Assert.AreEqual(2, list.Items.Count);
    }

    [TestMethod]
    public void Feed_TwoBlankLinesEndList()
    {
        var parser = new StreamParser();

        parser.Feed("- item1\n\n\n- item2\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        var list1 = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(1, list1.Items.Count);
        Assert.AreEqual("item1", GetItemText(list1.Items[0]));

        var list2 = (ListBlock)parser.Blocks[1];
        Assert.AreEqual(1, list2.Items.Count);
        Assert.AreEqual("item2", GetItemText(list2.Items[0]));
    }

    [TestMethod]
    public void Feed_MultiParagraphItem()
    {
        var parser = new StreamParser();

        parser.Feed("- para1\n\n  para2\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(1, list.Items.Count);
        var item = list.Items[0];
        // Blank line + continuation → two paragraphs inside the item
        Assert.IsTrue(item.Children.Count > 0);
    }

    // ═══════════════════════════════════════════
    //  List followed by paragraph
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_ListThenParagraph()
    {
        var parser = new StreamParser();

        parser.Feed("- item1\n\npara\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ListBlock));
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(ParagraphBlock));
    }

    [TestMethod]
    public void Feed_ListThenParagraph_NoBlankLine()
    {
        var parser = new StreamParser();

        parser.Feed("- item1\npara\n");

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ListBlock));
        Assert.IsInstanceOfType(parser.Blocks[1], typeof(ParagraphBlock));

        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(1, list.Items.Count);
        Assert.AreEqual("item1", GetItemText(list.Items[0]));
    }

    // ═══════════════════════════════════════════
    //  Different marker chars break lists
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_DifferentBulletChars_SeparateLists()
    {
        var parser = new StreamParser();

        parser.Feed("- item1\n* item2\n");

        Assert.AreEqual(2, parser.Blocks.Count);
    }

    [TestMethod]
    public void Feed_DifferentOrderedDelimiter_SeparateLists()
    {
        var parser = new StreamParser();

        parser.Feed("1. item1\n2) item2\n");

        Assert.AreEqual(2, parser.Blocks.Count);
    }

    // ═══════════════════════════════════════════
    //  Nested lists
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_NestedUnorderedList()
    {
        var parser = new StreamParser();

        parser.Feed("- outer\n  - inner\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var outerList = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(1, outerList.Items.Count);

        var outerItem = outerList.Items[0];
        // The inner list should be created inside the outer item's children
        Assert.IsTrue(outerItem.Children.Count >= 1, "Outer item should have children");

        // The first child should be a list (the nested one)
        var nestedList = outerItem.Children.OfType<ListBlock>().FirstOrDefault();
        Assert.IsNotNull(nestedList, "Should have a nested list");
        Assert.AreEqual(1, nestedList.Items.Count);
        Assert.AreEqual("inner", GetItemText(nestedList.Items[0]));
    }

    [TestMethod]
    public void Feed_NestedOrderedInUnordered()
    {
        var parser = new StreamParser();

        parser.Feed("- outer\n  1. inner\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var outerList = (ListBlock)parser.Blocks[0];
        var outerItem = outerList.Items[0];
        var nestedList = outerItem.Children.OfType<ListBlock>().FirstOrDefault();
        Assert.IsNotNull(nestedList);
        Assert.AreEqual(ListStyle.Ordered, nestedList.Style);
        Assert.AreEqual("inner", GetItemText(nestedList.Items[0]));
    }

    // ═══════════════════════════════════════════
    //  Indented markers
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_IndentedList()
    {
        var parser = new StreamParser();

        parser.Feed("  - item1\n  - item2\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(2, list.Items.Count);
    }

    [TestMethod]
    public void Feed_ContinuationWithIndentedList()
    {
        var parser = new StreamParser();

        parser.Feed("  - item\n    continuation\n");

        Assert.AreEqual(1, parser.Blocks.Count);
        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(1, list.Items.Count);
        Assert.AreEqual("item\ncontinuation", GetItemText(list.Items[0]));
    }

    // ═══════════════════════════════════════════
    //  Complete
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Complete_FinalizesList()
    {
        var parser = new StreamParser();

        parser.Feed("- item1\n- item2\n");
        Assert.AreEqual(1, parser.Blocks.Count, "Should have 1 block");
        Assert.IsInstanceOfType(parser.Blocks[0], typeof(ListBlock), "Block should be ListBlock");

        var listBefore = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(BlockStatus.Open, listBefore.Status, "List should be Open before Complete");
        Assert.AreEqual(2, listBefore.Items.Count, "Should have 2 items");

        parser.Complete();

        Assert.AreEqual(1, parser.Blocks.Count, "Should still have 1 block after Complete");
        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(2, list.Items.Count, "Should still have 2 items");
        Assert.AreEqual(BlockStatus.Finalized, list.Status, "List should be Finalized after Complete");

        foreach (var item in list.Items)
            Assert.AreEqual(BlockStatus.Finalized, item.Status);
    }

    [TestMethod]
    public void Complete_NestedListFinalized()
    {
        var parser = new StreamParser();

        parser.Feed("- outer\n  - inner\n");
        parser.Complete();

        var list = (ListBlock)parser.Blocks[0];
        var outerItem = list.Items[0];
        var nestedList = outerItem.Children.OfType<ListBlock>().First();
        Assert.AreEqual(BlockStatus.Finalized, nestedList.Status);
    }

    // ═══════════════════════════════════════════
    //  Char-by-char streaming (exercises _consumingFirstLine / _consumingContinuationLine)
    // ═══════════════════════════════════════════

    [TestMethod]
    public void Feed_CharByChar_SingleItem()
    {
        // Exercises branch 3 _itemParser.Feed(chunk) via _consumingFirstLine path.
        // After OnMatch+Append("- ", _consumingFirstLine = true), each subsequent
        // char hits _consumingFirstLine in Feed() and forwards to innerParser.
        var parser = new StreamParser();

        foreach (char c in "- Hello\n")
            parser.Feed(c.ToString());

        Assert.AreEqual(1, parser.Blocks.Count);
        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(ListStyle.Bullet, list.Style);
        Assert.AreEqual(1, list.Items.Count);
        Assert.AreEqual("Hello", GetItemText(list.Items[0]));
    }

    [TestMethod]
    public void Feed_CharByChar_WithContinuation()
    {
        // Exercises _consumingFirstLine for first item content,
        // then _pendingWs accumulation + _consumingContinuationLine for continuation.
        var parser = new StreamParser();

        foreach (char c in "- Hello\n  World\n")
            parser.Feed(c.ToString());

        var list = (ListBlock)parser.Blocks[0];
        Assert.AreEqual(1, list.Items.Count);
        Assert.AreEqual("Hello\nWorld", GetItemText(list.Items[0]));
    }

    // ═══════════════════════════════════════════
    //  Content verification helpers
    // ═══════════════════════════════════════════

    /// <summary>提取列表项中首段文本（不含换行）。</summary>
    private static string GetItemText(ListItemBlock item)
    {
        var para = item.Children.OfType<ParagraphBlock>().FirstOrDefault();
        return para?.Inlines?.RawBuffer?.ToString() ?? "";
    }
}
