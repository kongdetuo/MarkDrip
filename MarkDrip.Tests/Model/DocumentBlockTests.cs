using MarkDrip.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Model;

[TestClass]
public class DocumentBlockTests
{
    // ─── HeadingBlock ───

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(6)]
    public void HeadingBlock_Create_ValidLevel_SetsProperties(int level)
    {
        var heading = new HeadingBlock(level);

        Assert.AreEqual(BlockKind.Heading, heading.Kind);
        Assert.AreEqual(level, heading.Level);
        Assert.AreEqual(BlockStatus.Open, heading.Status);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(7)]
    [DataRow(-1)]
    public void HeadingBlock_Create_InvalidLevel_Throws(int level)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new HeadingBlock(level));
    }

    [TestMethod]
    public void HeadingBlock_HasUniqueId()
    {
        var h1 = new HeadingBlock(1);
        var h2 = new HeadingBlock(1);

        Assert.AreNotEqual(h1.Id, h2.Id);
    }

    [TestMethod]
    public void HeadingBlock_HasInlines()
    {
        var heading = new HeadingBlock(2);

        Assert.IsNotNull(heading.Inlines);
    }

    // ─── ParagraphBlock ───

    [TestMethod]
    public void ParagraphBlock_Create_SetsProperties()
    {
        var para = new ParagraphBlock();

        Assert.AreEqual(BlockKind.Paragraph, para.Kind);
        Assert.AreEqual(BlockStatus.Open, para.Status);
        Assert.IsNotNull(para.Inlines);
    }

    // ─── CodeBlock ───

    [TestMethod]
    public void CodeBlock_Create_SetsProperties()
    {
        var code = new CodeBlock();

        Assert.AreEqual(BlockKind.CodeBlock, code.Kind);
        Assert.AreEqual(BlockStatus.Open, code.Status);
        Assert.IsNull(code.InfoString);
        Assert.IsNotNull(code.Content);
        Assert.AreEqual(0, code.Content.Length);
    }

    [TestMethod]
    public void CodeBlock_InfoString_CanBeSet()
    {
        var code = new CodeBlock();

        code.InfoString = "csharp";

        Assert.AreEqual("csharp", code.InfoString);
    }

    [TestMethod]
    public void CodeBlock_Content_CanBeAppended()
    {
        var code = new CodeBlock();

        code.Content.Append("var x = 1;");

        Assert.AreEqual("var x = 1;", code.Content.ToString());
    }

    // ─── ThematicBreakBlock ───

    [TestMethod]
    public void ThematicBreakBlock_Create_IsFinalized()
    {
        var hr = new ThematicBreakBlock();

        Assert.AreEqual(BlockKind.ThematicBreak, hr.Kind);
        Assert.AreEqual(BlockStatus.Finalized, hr.Status);
    }

    // ─── ListBlock ───

    [TestMethod]
    public void ListBlock_Create_Bullet_SetsProperties()
    {
        var list = new ListBlock(ListStyle.Bullet);

        Assert.AreEqual(BlockKind.List, list.Kind);
        Assert.AreEqual(ListStyle.Bullet, list.Style);
        Assert.AreEqual(BlockStatus.Open, list.Status);
        Assert.AreEqual(0, list.Items.Count);
    }

    [TestMethod]
    public void ListBlock_Create_Ordered_SetsProperties()
    {
        var list = new ListBlock(ListStyle.Ordered);

        Assert.AreEqual(ListStyle.Ordered, list.Style);
    }

    // ─── ListItemBlock ───

    [TestMethod]
    public void ListItemBlock_Create_SetsProperties()
    {
        var item = new ListItemBlock();

        Assert.AreEqual(BlockKind.ListItem, item.Kind);
        Assert.AreEqual(BlockStatus.Open, item.Status);
        Assert.AreEqual(0, item.Children.Count);
    }

    // ─── BlockQuoteBlock ───

    [TestMethod]
    public void BlockQuoteBlock_Create_SetsProperties()
    {
        var quote = new BlockQuoteBlock();

        Assert.AreEqual(BlockKind.BlockQuote, quote.Kind);
        Assert.AreEqual(BlockStatus.Open, quote.Status);
        Assert.AreEqual(0, quote.Children.Count);
    }

    // ─── Status Transitions ───

    [TestMethod]
    public void BlockStatus_CanTransition_ToFinalized()
    {
        var para = new ParagraphBlock();

        para.Status = BlockStatus.Finalized;

        Assert.AreEqual(BlockStatus.Finalized, para.Status);
    }

    [TestMethod]
    public void BlockStatus_Finalized_IsIdempotent()
    {
        var para = new ParagraphBlock();
        para.Status = BlockStatus.Finalized;

        para.Status = BlockStatus.Finalized; // should not throw

        Assert.AreEqual(BlockStatus.Finalized, para.Status);
    }
}
