using MarkDrip.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Model;

[TestClass]
public class MutableMarkdownDocumentTests
{
    [TestMethod]
    public void NewDocument_IsEmpty()
    {
        var doc = new MutableMarkdownDocument();

        Assert.IsTrue(doc.IsEmpty);
        Assert.AreEqual(0, doc.Blocks.Count);
        Assert.IsNull(doc.LastBlock);
    }

    [TestMethod]
    public void AddBlock_DocumentIsNotEmpty()
    {
        var doc = new MutableMarkdownDocument();

        doc.Blocks.Add(new ParagraphBlock());

        Assert.IsFalse(doc.IsEmpty);
        Assert.AreEqual(1, doc.Blocks.Count);
    }

    [TestMethod]
    public void LastBlock_ReturnsLastAddedBlock()
    {
        var doc = new MutableMarkdownDocument();

        doc.Blocks.Add(new ParagraphBlock());
        doc.Blocks.Add(new HeadingBlock(2));

        var last = doc.LastBlock;
        Assert.IsInstanceOfType(last, typeof(HeadingBlock));
        Assert.AreEqual(2, ((HeadingBlock)last).Level);
    }

    [TestMethod]
    public void Blocks_OrderIsPreserved()
    {
        var doc = new MutableMarkdownDocument();

        var h1 = new HeadingBlock(1);
        var p = new ParagraphBlock();
        var h2 = new HeadingBlock(2);

        doc.Blocks.Add(h1);
        doc.Blocks.Add(p);
        doc.Blocks.Add(h2);

        Assert.AreEqual(3, doc.Blocks.Count);
        Assert.AreSame(h1, doc.Blocks[0]);
        Assert.AreSame(p, doc.Blocks[1]);
        Assert.AreSame(h2, doc.Blocks[2]);
    }

    [TestMethod]
    public void LastBlock_Null_WhenDocumentEmpty()
    {
        var doc = new MutableMarkdownDocument();

        Assert.IsNull(doc.LastBlock);
    }

    [TestMethod]
    public void ObservableCollection_SupportsCollectionChanged()
    {
        var doc = new MutableMarkdownDocument();
        int changeCount = 0;

        doc.Blocks.CollectionChanged += (_, _) => changeCount++;

        doc.Blocks.Add(new ParagraphBlock());
        doc.Blocks.Add(new HeadingBlock(1));

        Assert.AreEqual(2, changeCount);
    }
}
