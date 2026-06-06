using MarkDrip.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Model;

[TestClass]
public class BlockStatusTests
{
    [TestMethod]
    public void BlockStatus_HasExpectedMembers()
    {
        Assert.AreEqual(0, (int)BlockStatus.Open);
        Assert.AreEqual(1, (int)BlockStatus.Finalized);
    }
}
