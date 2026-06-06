using MarkDrip.Model;
using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class StreamParserCompleteTests
{
    [TestMethod]
    public void Complete_FinalizesOpenParagraph()
    {
        var parser = new StreamParser();
        parser.Feed("Hello\n");

        parser.Complete();

        Assert.AreEqual(1, parser.Blocks.Count);
        var para = (ParagraphBlock)parser.Blocks[0];
        Assert.AreEqual(BlockStatus.Finalized, para.Status);
    }

    [TestMethod]
    public void Complete_FinalizesParagraphWithoutTrailingNewline()
    {
        var parser = new StreamParser();
        parser.Feed("Hello");

        parser.Complete();

        Assert.AreEqual(1, parser.Blocks.Count);
        var para = (ParagraphBlock)parser.Blocks[0];
        Assert.AreEqual(BlockStatus.Finalized, para.Status);
        Assert.AreEqual("Hello", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Complete_BufferedPartialMatch_FlushesAsParagraph()
    {
        // "#" 没有换行符 → AtxHeader 返回 PartialMatch（hashCount == line.Length）
        // lineBuffer 残留 "#"，Complete 应将其作为段落文本处理
        var parser = new StreamParser();
        parser.Feed("#");

        parser.Complete();

        Assert.AreEqual(1, parser.Blocks.Count);
        var para = parser.Blocks[0] as ParagraphBlock;
        Assert.IsNotNull(para, "PartialMatch content should become a paragraph");
        Assert.AreEqual("#", GetInlinesText(para.Inlines));
    }

    [TestMethod]
    public void Complete_MultipleFeedCalls_FinalizesEverything()
    {
        var parser = new StreamParser();
        parser.Feed("Line one\n");
        parser.Feed("\n");  // blank line separates paragraphs
        parser.Feed("Line two\n");

        parser.Complete();

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.AreEqual(BlockStatus.Finalized, parser.Blocks[0].Status);
        Assert.AreEqual(BlockStatus.Finalized, parser.Blocks[1].Status);
    }

    [TestMethod]
    public void Complete_BlockQuoteInner_PropagatesFinalization()
    {
        var parser = new StreamParser();
        parser.Feed("> Quoted text\n");

        parser.Complete();

        var quote = (BlockQuoteBlock)parser.Blocks[0];
        Assert.AreEqual(BlockStatus.Finalized, quote.Status);
        Assert.AreEqual(1, quote.Children.Count);
        var innerPara = (ParagraphBlock)quote.Children[0];
        Assert.AreEqual(BlockStatus.Finalized, innerPara.Status);
    }

    [TestMethod]
    public void Complete_EmptyParser_NoCrash()
    {
        var parser = new StreamParser();

        parser.Complete(); // should not throw

        Assert.AreEqual(0, parser.Blocks.Count);
    }

    [TestMethod]
    public void Complete_SealsInlines()
    {
        var parser = new StreamParser();
        parser.Feed("Hello\n");

        parser.Complete();

        var para = (ParagraphBlock)parser.Blocks[0];
        // Seal 确保了 GetInlines 返回已解析的结果
        var inlines = para.Inlines.GetInlines();
        Assert.AreEqual(1, inlines.Count);
        var run = inlines[0] as TextRun;
        Assert.IsNotNull(run);
        Assert.AreEqual("Hello", run.Text);
    }

    [TestMethod]
    public void Complete_HeadingFromSetext_Finalized()
    {
        var parser = new StreamParser();
        parser.Feed("Title\n====\n");

        parser.Complete();

        var heading = (HeadingBlock)parser.Blocks[0];
        Assert.AreEqual(BlockStatus.Finalized, heading.Status);
    }

    [TestMethod]
    public void Complete_MixedBlocks_FinalizesAll()
    {
        var parser = new StreamParser();
        parser.Feed("# Heading\n");
        parser.Feed("\n");       // blank line ends heading
        parser.Feed("> Quote\n");
        parser.Feed("\n");       // blank line ends quote
        parser.Feed("Paragraph\n");

        parser.Complete();

        Assert.AreEqual(3, parser.Blocks.Count);
        foreach (var block in parser.Blocks)
            Assert.AreEqual(BlockStatus.Finalized, block.Status, $"Block {block.Kind} should be Finalized");
    }

    [TestMethod]
    public void Complete_BlockQuoteWithNestedSetext_FinalizesAll()
    {
        var parser = new StreamParser();
        parser.Feed("> Title\n> ====\n");

        parser.Complete();

        var quote = (BlockQuoteBlock)parser.Blocks[0];
        var heading = (HeadingBlock)quote.Children[0];
        Assert.AreEqual(BlockStatus.Finalized, heading.Status);
        Assert.AreEqual(1, heading.Level);
        Assert.AreEqual("Title", GetInlinesText(heading.Inlines));
    }

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
}
