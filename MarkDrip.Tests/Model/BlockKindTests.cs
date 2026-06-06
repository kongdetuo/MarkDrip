using MarkDrip.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Model;

[TestClass]
public class BlockKindTests
{
    [TestMethod]
    public void BlockKind_HasExpectedMembers()
    {
        // Verify all expected block kinds exist
        Assert.AreEqual(0, (int)BlockKind.Paragraph);
        Assert.AreEqual(1, (int)BlockKind.Heading);
        Assert.AreEqual(2, (int)BlockKind.CodeBlock);
        Assert.AreEqual(3, (int)BlockKind.ThematicBreak);
        Assert.AreEqual(4, (int)BlockKind.List);
        Assert.AreEqual(5, (int)BlockKind.ListItem);
        Assert.AreEqual(6, (int)BlockKind.BlockQuote);
    }

    [TestMethod]
    public void BlockKind_Values_AreDistinct()
    {
        var values = Enum.GetValues<BlockKind>();
        Assert.AreEqual(values.Length, values.Distinct().Count());
    }
}
