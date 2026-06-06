using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class LineBufferTests
{
    // ─── Append & Length ───

    [TestMethod]
    public void Append_Empty_LengthZero()
    {
        var buffer = new LineBuffer();
        buffer.Append("");
        Assert.AreEqual(0, buffer.Length);
    }

    [TestMethod]
    public void Append_SingleChar_LengthOne()
    {
        var buffer = new LineBuffer();
        buffer.Append("a");
        Assert.AreEqual(1, buffer.Length);
    }

    [TestMethod]
    public void Append_MultipleChars_LengthMatches()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello");
        Assert.AreEqual(5, buffer.Length);
    }

    [TestMethod]
    public void Append_Chunks_AccumulatesLength()
    {
        var buffer = new LineBuffer();
        buffer.Append("hel");
        Assert.AreEqual(3, buffer.Length);
        buffer.Append("lo");
        Assert.AreEqual(5, buffer.Length);
        buffer.Append(" world");
        Assert.AreEqual(11, buffer.Length);
    }

    // ─── Slice (Single Segment — Fast Path) ───

    [TestMethod]
    public void Slice_EntireContent()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello world");
        Assert.AreEqual("hello world", buffer.Slice(0, buffer.Length).ToString());
    }

    [TestMethod]
    public void Slice_Prefix()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello world");
        Assert.AreEqual("hello", buffer.Slice(0, 5).ToString());
    }

    [TestMethod]
    public void Slice_Suffix()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello world");
        Assert.AreEqual("world", buffer.Slice(6, 5).ToString());
    }

    [TestMethod]
    public void Slice_MidSection()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello world");
        Assert.AreEqual("lo wo", buffer.Slice(3, 5).ToString());
    }

    [TestMethod]
    public void Slice_ZeroLength_ReturnsEmpty()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello");
        Assert.AreEqual(0, buffer.Slice(0, 0).Length);
        Assert.AreEqual(0, buffer.Slice(3, 0).Length);
    }

    [TestMethod]
    public void Slice_PartialPrefixAtEndOfChunk()
    {
        var buffer = new LineBuffer();
        // Fill segment with 1020 'a's, then add "> x"
        buffer.Append(new string('a', 1020));
        buffer.Append("> x");
        // "> x" should be entirely within segment
        Assert.AreEqual(">", buffer.Slice(1020, 1).ToString());
        Assert.AreEqual("> ", buffer.Slice(1020, 2).ToString());
        Assert.AreEqual("> x", buffer.Slice(1020, 3).ToString());
    }

    // ─── Slice (Cross Segment — Slow Path) ───

    [TestMethod]
    public void Slice_CrossSegmentBoundary()
    {
        var buffer = new LineBuffer();
        // Fill first segment exactly (1024 chars)
        buffer.Append(new string('a', 1022));
        buffer.Append("bc");
        // Now length = 1024, all within first segment
        // Add more to spill into second segment
        buffer.Append("def");

        // "bc" spans end of first segment
        Assert.AreEqual("bc", buffer.Slice(1022, 2).ToString());
        // "cde" crosses the boundary
        var cross = buffer.Slice(1023, 3).ToString();
        Assert.AreEqual("cde", cross);
    }

    [TestMethod]
    public void Slice_LargeContent_ReturnsCorrectContent()
    {
        var buffer = new LineBuffer();
        var expected = new string('x', 3000);
        buffer.Append(expected);

        Assert.AreEqual(3000, buffer.Length);
        var slice = buffer.Slice(0, 3000).ToString();
        Assert.AreEqual(expected, slice);
    }

    [TestMethod]
    public void Slice_SuffixOfLargeContent()
    {
        var buffer = new LineBuffer();
        buffer.Append(new string('a', 2048));
        buffer.Append("tail");

        Assert.AreEqual("tail", buffer.Slice(2048, 4).ToString());
    }

    [TestMethod]
    public void Slice_CrossSegment_AtExactSegmentBoundary()
    {
        var buffer = new LineBuffer();
        // First segment: 1023 'x' + "y" = 1024 chars
        buffer.Append(new string('x', 1023));
        buffer.Append("yz"); // y goes to first segment end, z spills to second

        // "yz" crosses boundary at index 1023
        Assert.AreEqual("yz", buffer.Slice(1023, 2).ToString());
    }

    // ─── Clear ───

    [TestMethod]
    public void Clear_AfterAppend_ResetsLength()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello");
        buffer.Clear();
        Assert.AreEqual(0, buffer.Length);
    }

    [TestMethod]
    public void Clear_ThenReAppend_WorksCorrectly()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello");
        buffer.Clear();
        Assert.AreEqual(0, buffer.Length);

        buffer.Append("world");
        Assert.AreEqual(5, buffer.Length);
        Assert.AreEqual("world", buffer.Slice(0, 5).ToString());
    }

    [TestMethod]
    public void Clear_DoubleClear_NoError()
    {
        var buffer = new LineBuffer();
        buffer.Clear();
        buffer.Clear();
        Assert.AreEqual(0, buffer.Length);
    }

    [TestMethod]
    public void Clear_ThenLargeAppend_Ok()
    {
        var buffer = new LineBuffer();
        buffer.Append(new string('a', 500));
        buffer.Clear();
        buffer.Append(new string('b', 2000));
        Assert.AreEqual(2000, buffer.Length);
        Assert.AreEqual('b', buffer.Slice(0, 1)[0]);
        Assert.AreEqual('b', buffer.Slice(1999, 1)[0]);
    }

    // ─── Argument Validation ───

    [TestMethod]
    public void Slice_NegativeStart_Throws()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello");
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => buffer.Slice(-1, 1));
    }

    [TestMethod]
    public void Slice_NegativeLength_Throws()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello");
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => buffer.Slice(0, -1));
    }

    [TestMethod]
    public void Slice_StartBeyondLength_Throws()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello");
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => buffer.Slice(10, 1));
    }

    [TestMethod]
    public void Slice_StartPlusLengthExceedsLength_Throws()
    {
        var buffer = new LineBuffer();
        buffer.Append("hello");
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => buffer.Slice(3, 5));
    }

    [TestMethod]
    public void Slice_EmptyBuffer_Throws()
    {
        var buffer = new LineBuffer();
        // Empty buffer: any non-zero Slice should throw
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => buffer.Slice(0, 1));
        // But zero-length is OK (no-op)
        Assert.AreEqual(0, buffer.Slice(0, 0).Length);
    }

    // ─── Varied Append Patterns ───

    [TestMethod]
    public void Append_ManySmallChunks()
    {
        var buffer = new LineBuffer();
        for (int i = 0; i < 100; i++)
            buffer.Append("a");

        Assert.AreEqual(100, buffer.Length);
        Assert.AreEqual(100, buffer.Slice(0, 100).Length);
        // All chars should be 'a'
        foreach (var ch in buffer.Slice(0, 100))
            Assert.AreEqual('a', ch);
    }

    [TestMethod]
    public void Append_JustUnderSegmentBoundary()
    {
        var buffer = new LineBuffer();
        var content = new string('x', 1023);
        buffer.Append(content);
        Assert.AreEqual(1023, buffer.Length);
        Assert.AreEqual(content, buffer.Slice(0, 1023).ToString());
    }

    [TestMethod]
    public void Append_ExactlySegmentSize()
    {
        var buffer = new LineBuffer();
        var content = new string('x', 1024);
        buffer.Append(content);
        Assert.AreEqual(1024, buffer.Length);
        Assert.AreEqual(content, buffer.Slice(0, 1024).ToString());
    }

    [TestMethod]
    public void Append_JustOverSegmentBoundary()
    {
        var buffer = new LineBuffer();
        buffer.Append(new string('x', 1023));
        buffer.Append("!");
        Assert.AreEqual(1024, buffer.Length);
        Assert.AreEqual('!', buffer.Slice(1023, 1)[0]);
    }

    [TestMethod]
    public void Append_SpanOverload_Works()
    {
        var buffer = new LineBuffer();
        ReadOnlySpan<char> span = "hello".AsSpan();
        buffer.Append(span);
        Assert.AreEqual(5, buffer.Length);
        Assert.AreEqual("hello", buffer.Slice(0, 5).ToString());
    }

    [TestMethod]
    public void Slice_AfterMultipleClears_ReturnsCorrect()
    {
        var buffer = new LineBuffer();
        buffer.Append("first version");
        buffer.Clear();
        buffer.Append("second version");

        // Ensure no stale cross-segment scratch data
        Assert.AreEqual("second version", buffer.Slice(0, buffer.Length).ToString());
    }

    // ─── Prefix match (common use case) ───

    [TestMethod]
    public void Slice_FiveCharPrefix_Match()
    {
        var buffer = new LineBuffer();
        buffer.Append("> hello");
        var head = buffer.Slice(0, Math.Min(5, buffer.Length));
        Assert.AreEqual("> hel", head.ToString());
    }

    [TestMethod]
    public void Slice_ShortContent_FiveCharPrefix()
    {
        var buffer = new LineBuffer();
        buffer.Append(">");
        // When content < 5, take actual length
        var head = buffer.Slice(0, Math.Min(5, buffer.Length));
        Assert.AreEqual(">", head.ToString());
    }
}
