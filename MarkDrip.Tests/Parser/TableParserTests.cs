using MarkDrip.Model;
using MarkDrip.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkDrip.Tests.Parser;

[TestClass]
public class TableParserTests
{
    [TestMethod]
    public void BasicTable_ParseHeaderDelimiterAndDataRow()
    {
        var parser = new StreamParser();
        parser.Feed("| A | B |\n");
        parser.Feed("|---|---|\n");
        parser.Feed("| C | D |\n");
        parser.Complete();

        var table = (TableBlock)parser.Blocks[0];
        Assert.AreEqual(2, table.Rows.Count);
        Assert.AreEqual(2, table.Alignments.Length);
        Assert.AreEqual(ColumnAlignment.None, table.Alignments[0]);
        Assert.AreEqual(ColumnAlignment.None, table.Alignments[1]);
        Assert.AreEqual("A", GetCellText(table.Rows[0].Cells[0]));
        Assert.AreEqual("B", GetCellText(table.Rows[0].Cells[1]));
        Assert.AreEqual("C", GetCellText(table.Rows[1].Cells[0]));
        Assert.AreEqual("D", GetCellText(table.Rows[1].Cells[1]));
    }

    [TestMethod]
    public void TableWithAlignment()
    {
        var parser = new StreamParser();
        parser.Feed("| Left | Center | Right |\n");
        parser.Feed("|:----|:----:|---:|\n");
        parser.Feed("|  A   |   B   |  C   |\n");
        parser.Complete();

        var table = (TableBlock)parser.Blocks[0];
        Assert.AreEqual(3, table.Alignments.Length);
        Assert.AreEqual(ColumnAlignment.Left, table.Alignments[0]);
        Assert.AreEqual(ColumnAlignment.Center, table.Alignments[1]);
        Assert.AreEqual(ColumnAlignment.Right, table.Alignments[2]);
    }

    [TestMethod]
    public void TableWithMultipleDataRows()
    {
        var parser = new StreamParser();
        parser.Feed("| H1 | H2 |\n");
        parser.Feed("|---|---|\n");
        parser.Feed("| A1 | A2 |\n");
        parser.Feed("| B1 | B2 |\n");
        parser.Feed("| C1 | C2 |\n");
        parser.Complete();

        var table = (TableBlock)parser.Blocks[0];
        Assert.AreEqual(4, table.Rows.Count);
        Assert.AreEqual("C2", GetCellText(table.Rows[3].Cells[1]));
    }

    [TestMethod]
    public void TableEndsWithBlankLine()
    {
        var parser = new StreamParser();
        parser.Feed("| H |\n");
        parser.Feed("|---|\n");
        parser.Feed("| D |\n");
        parser.Feed("\n");
        parser.Feed("Paragraph\n");
        parser.Complete();

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.AreEqual(BlockKind.Table, parser.Blocks[0].Kind);
        Assert.AreEqual(BlockKind.Paragraph, parser.Blocks[1].Kind);
        var table = (TableBlock)parser.Blocks[0];
        Assert.AreEqual(2, table.Rows.Count);
    }

    [TestMethod]
    public void TableEndsWithNonPipeLine()
    {
        var parser = new StreamParser();
        parser.Feed("| H |\n");
        parser.Feed("|---|\n");
        parser.Feed("| D |\n");
        parser.Feed("Not a table row\n");
        parser.Complete();

        Assert.AreEqual(2, parser.Blocks.Count);
        Assert.AreEqual(BlockKind.Table, parser.Blocks[0].Kind);
        Assert.AreEqual(2, ((TableBlock)parser.Blocks[0]).Rows.Count);
    }

    [TestMethod]
    public void TableInSingleChunk()
    {
        var parser = new StreamParser();
        parser.Feed("| A | B |\n|---|---|\n| C | D |\n");
        parser.Complete();

        var table = (TableBlock)parser.Blocks[0];
        Assert.AreEqual(2, table.Rows.Count);
        Assert.AreEqual("A", GetCellText(table.Rows[0].Cells[0]));
        Assert.AreEqual("C", GetCellText(table.Rows[1].Cells[0]));
    }

    [TestMethod]
    public void TableCharByChar()
    {
        var parser = new StreamParser();
        var text = "| A | B |\n|---|---|\n| C | D |\n";
        foreach (char c in text)
            parser.Feed(c.ToString());

        var table = (TableBlock)parser.Blocks[0];
        Assert.AreEqual(2, table.Rows.Count);
        Assert.AreEqual("A", GetCellText(table.Rows[0].Cells[0]));
        Assert.AreEqual("D", GetCellText(table.Rows[1].Cells[1]));
    }

    [TestMethod]
    public void HeaderTextIsNotPipe_DoesNotMatch()
    {
        var parser = new StreamParser();
        parser.Feed("Hello\n");
        parser.Feed("|---|---|\n");
        parser.Complete();

        // "Hello" has no |, so not a table — the delimiter row becomes a paragraph
        Assert.IsFalse(parser.Blocks.Any(b => b.Kind == BlockKind.Table));
    }

    [TestMethod]
    public void ColumnCountMismatch_PadsExtraCells()
    {
        var parser = new StreamParser();
        parser.Feed("| A | B |\n");
        parser.Feed("|---|---|\n");
        parser.Feed("| C |\n");
        parser.Complete();

        var table = (TableBlock)parser.Blocks[0];
        Assert.AreEqual(2, table.Rows.Count);
        Assert.AreEqual(2, table.Rows[1].Cells.Count);
        Assert.AreEqual("C", GetCellText(table.Rows[1].Cells[0]));
        Assert.AreEqual("", GetCellText(table.Rows[1].Cells[1]));
    }

    [TestMethod]
    public void ColumnCountMismatch_TrimsExtraCells()
    {
        var parser = new StreamParser();
        parser.Feed("| A | B |\n");
        parser.Feed("|---|---|\n");
        parser.Feed("| C | D | E | F |\n");
        parser.Complete();

        var table = (TableBlock)parser.Blocks[0];
        Assert.AreEqual(2, table.Rows.Count);
        Assert.AreEqual(2, table.Rows[1].Cells.Count);
    }

    [TestMethod]
    public void NoLeadingOrTrailingPipe_StillParses()
    {
        var parser = new StreamParser();
        parser.Feed(" A | B \n");
        parser.Feed("---|--\n");
        parser.Complete();

        var table = (TableBlock)parser.Blocks[0];
        Assert.AreEqual(1, table.Rows.Count);
        Assert.AreEqual("A", GetCellText(table.Rows[0].Cells[0]));
        Assert.AreEqual("B", GetCellText(table.Rows[0].Cells[1]));
    }

    private static string GetCellText(TableCell cell)
    {
        var elements = cell.Inlines.GetInlines();
        if (elements.Count == 0) return "";
        return string.Concat(elements.Select(e => e is TextRun t ? t.Text : ""));
    }
}
