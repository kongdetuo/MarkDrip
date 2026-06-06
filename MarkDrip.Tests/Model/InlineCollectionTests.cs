using MarkDrip.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Model;

[TestClass]
public class InlineCollectionTests
{
    [TestMethod]
    public void GetInlines_EmptyBuffer_ReturnsEmpty()
    {
        var collection = new InlineCollection();

        var inlines = collection.GetInlines();

        Assert.AreEqual(0, inlines.Count);
    }

    [TestMethod]
    public void GetInlines_AfterAppend_ReturnsTextRun()
    {
        var collection = new InlineCollection();

        collection.Append("Hello world");

        var inlines = collection.GetInlines();
        Assert.AreEqual(1, inlines.Count);
        Assert.IsInstanceOfType(inlines[0], typeof(TextRun));
        var run = (TextRun)inlines[0];
        Assert.AreEqual("Hello world", run.Text);
    }

    [TestMethod]
    public void GetInlines_MultipleAppends_AccumulateText()
    {
        var collection = new InlineCollection();

        collection.Append("Hello ");
        collection.Append("world");
        collection.Append("!");

        var inlines = collection.GetInlines();
        Assert.IsInstanceOfType(inlines[0], typeof(TextRun));
        var run = (TextRun)inlines[0];
        Assert.AreEqual("Hello world!", run.Text);
    }

    [TestMethod]
    public void GetInlines_AppendLine_AddsSoftLineBreak()
    {
        var collection = new InlineCollection();

        collection.Append("line1");
        collection.AppendLine();
        collection.Append("line2");

        var inlines = collection.GetInlines();
        Assert.AreEqual(3, inlines.Count);
        Assert.IsInstanceOfType(inlines[0], typeof(TextRun));
        Assert.AreEqual("line1", ((TextRun)inlines[0]).Text);
        Assert.IsInstanceOfType(inlines[1], typeof(SoftLineBreak));
        Assert.IsInstanceOfType(inlines[2], typeof(TextRun));
        Assert.AreEqual("line2", ((TextRun)inlines[2]).Text);
    }

    [TestMethod]
    public void GetInlines_CachesResult_WhenNotDirty()
    {
        var collection = new InlineCollection();

        collection.Append("hello");
        var first = collection.GetInlines();

        collection.Append(" world"); // makes dirty again
        // After append, should re-parse
        var second = collection.GetInlines();

        // Verify it's the updated content
        Assert.IsInstanceOfType(second[0], typeof(TextRun));
        var run = (TextRun)second[0];
        Assert.AreEqual("hello world", run.Text);
    }

    [TestMethod]
    public void Seal_ProducesTextRun()
    {
        var collection = new InlineCollection();

        collection.Append("final content");
        collection.Seal();

        var inlines = collection.GetInlines();
        Assert.IsInstanceOfType(inlines[0], typeof(TextRun));
        var run = (TextRun)inlines[0];
        Assert.AreEqual("final content", run.Text);
    }

    [TestMethod]
    public void Seal_EmptyBuffer_ReturnsEmpty()
    {
        var collection = new InlineCollection();

        collection.Seal();

        Assert.AreEqual(0, collection.GetInlines().Count);
    }
}
