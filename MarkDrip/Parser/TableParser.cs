using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class TableParser : IBlockParser
{
    private TableBlock? _table;
    private bool _skipFirstLine;
    private TableRow? _pendingRow;
    private int _cellIndex = -1;

    public MatchResult TryMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        if (line.IndexOf('|') < 0)
            return MatchResult.NoMatch;

        if (context.PreviousBlock is not ParagraphBlock { Status: BlockStatus.Open } para)
            return MatchResult.NoMatch;

        if (!para.Inlines.RawBuffer.ToString().Contains('|'))
            return MatchResult.NoMatch;

        // Quick check: all non-whitespace, non-pipe chars so far must be valid delimiter chars (- or :)
        if (!CouldBeDelimiterLine(line))
            return MatchResult.NoMatch;

        // Without \n, the line is still accumulating — wait for more input
        if (line.IndexOf('\n') < 0)
            return MatchResult.PartialMatch;

        var trimmed = StripOuterPipes(TextUtils.StripAllTrailingNewlines(line).Trim());
        if (trimmed.Length == 0 || !IsValidDelimiterRow(trimmed))
            return MatchResult.NoMatch;

        return MatchResult.FullMatch;
    }

    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        if (context.PreviousBlock is not ParagraphBlock para)
            return;

        var headerText = para.Inlines.RawBuffer.ToString().Trim();

        var delimiterLine = StripOuterPipes(TextUtils.StripAllTrailingNewlines(line).Trim());
        var alignments = ParseAlignments(delimiterLine);

        _table = new TableBlock();
        _table.Alignments = alignments;

        var headerRow = ParseRow(headerText);
        NormalizeRowCells(headerRow, alignments.Length);
        _table.Rows.Add(headerRow);

        int idx = context.Blocks.IndexOf(para);
        context.Blocks.RemoveAt(idx);
        context.Blocks.Insert(idx, _table);

        _skipFirstLine = true;
        _pendingRow = null;
        _cellIndex = -1;
    }

    public AppendResult Append(TextChuck chunk, ParserContext context)
    {
        if (_skipFirstLine)
        {
            _skipFirstLine = false;
            return AppendResult.KeepFeeding;
        }

        var line = context.LastLineBuffer.AsSpan();
        if (line.Length == 0)
            return AppendResult.KeepFeeding;

        // Complete line (ends with \n)
        if (line[^1] == '\n')
        {
            var trimmed = TextUtils.StripTrailingNewline(line);

            if (_pendingRow != null)
            {
                // Row was already created via streaming; trim last cell and sync cell count
                if (_cellIndex >= 0 && _cellIndex < _pendingRow.Cells.Count)
                    TrimCellTrailingContent(_pendingRow.Cells[_cellIndex]);
                NormalizeRowCells(_pendingRow, _table!.Alignments.Length);
                _pendingRow = null;
                _cellIndex = -1;
                _table.NotifyContentChanged();
                return AppendResult.KeepFeeding;
            }

            if (TextUtils.IsBlankLine(trimmed))
                return AppendResult.YieldLine;

            if (trimmed.IndexOf('|') < 0)
                return AppendResult.YieldLine;

            var row = ParseRow(trimmed);
            NormalizeRowCells(row, _table!.Alignments.Length);
            _table!.Rows.Add(row);
            _table.NotifyContentChanged();
            return AppendResult.KeepFeeding;
        }

        // ---- Incomplete line: stream character by character ----

        // Check if this line belongs to the table
        var trimmedStart = line.TrimStart();
        if (trimmedStart.Length == 0)
            return AppendResult.KeepFeeding;

        if (trimmedStart[0] != '|')
        {
            // Not a table row — yield if we haven't started streaming,
            // otherwise the table was already growing
            if (_pendingRow == null)
                return AppendResult.YieldLine;
            return AppendResult.YieldLine;
        }

        // Start streaming a new row if not already started
        bool contentStarted;
        if (_pendingRow == null)
        {
            _pendingRow = new TableRow();
            int colCount = _table!.Alignments.Length;
            if (colCount == 0) colCount = 1;
            for (int i = 0; i < colCount; i++)
                _pendingRow.Cells.Add(new TableCell());
            _cellIndex = -1;
            contentStarted = false;
            _table.Rows.Add(_pendingRow);
            _table.NotifyContentChanged();
        }
        else
            contentStarted = _cellIndex >= 0 && _pendingRow.Cells[_cellIndex].Inlines.RawBuffer.Length > 0;

        // Process each character in the chunk
        foreach (char c in chunk.Text.ToString())
        {
            if (c == '|')
            {
                // Trim trailing whitespace from the previous cell
                if (_cellIndex >= 0 && _cellIndex < _pendingRow.Cells.Count)
                    TrimCellTrailingContent(_pendingRow.Cells[_cellIndex]);
                _cellIndex++;
                contentStarted = false;
            }
            else if (c is '\r')
            {
                // ignore carriage returns in streaming
            }
            else if (!contentStarted && char.IsWhiteSpace(c))
            {
                // skip leading whitespace in a cell
            }
            else
            {
                contentStarted = true;
                if (_cellIndex >= 0 && _cellIndex < _pendingRow.Cells.Count)
                    _pendingRow.Cells[_cellIndex].Inlines.Append(c.ToString());
            }
        }

        _table!.NotifyContentChanged();
        return AppendResult.KeepFeeding;
    }

    private static ReadOnlySpan<char> StripOuterPipes(ReadOnlySpan<char> s)
    {
        if (s.Length > 0 && s[0] == '|')
            s = s[1..];
        if (s.Length > 0 && s[^1] == '|')
            s = s[..^1];
        return s.Trim();
    }

    private static bool IsValidDelimiterRow(ReadOnlySpan<char> line)
    {
        if (line.Length == 0) return false;

        while (line.Length > 0)
        {
            int pipeIdx = line.IndexOf('|');
            ReadOnlySpan<char> cell;
            if (pipeIdx < 0)
            {
                cell = line.Trim();
                line = [];
            }
            else
            {
                cell = line[..pipeIdx].Trim();
                line = line[(pipeIdx + 1)..];
            }

            if (!IsValidDelimiterCell(cell))
                return false;
        }

        return true;
    }

    private static bool IsValidDelimiterCell(ReadOnlySpan<char> cell)
    {
        if (cell.Length == 0) return false;
        bool hasDash = false;
        for (int i = 0; i < cell.Length; i++)
        {
            var c = cell[i];
            if (c == '-')
                hasDash = true;
            else if (c != ':' && c != ' ')
                return false;
        }
        return hasDash;
    }

    private static ColumnAlignment[] ParseAlignments(ReadOnlySpan<char> line)
    {
        var result = new List<ColumnAlignment>();

        while (line.Length > 0)
        {
            int pipeIdx = line.IndexOf('|');
            ReadOnlySpan<char> cell;
            if (pipeIdx < 0)
            {
                cell = line.Trim();
                line = [];
            }
            else
            {
                cell = line[..pipeIdx].Trim();
                line = line[(pipeIdx + 1)..];
            }

            if (cell.Length == 0)
            {
                result.Add(ColumnAlignment.None);
                continue;
            }

            bool left = cell[0] == ':';
            bool right = cell[^1] == ':';

            if (left && right)
                result.Add(ColumnAlignment.Center);
            else if (left)
                result.Add(ColumnAlignment.Left);
            else if (right)
                result.Add(ColumnAlignment.Right);
            else
                result.Add(ColumnAlignment.None);
        }

        return [.. result];
    }

    private static TableRow ParseRow(ReadOnlySpan<char> line)
    {
        var trimmed = StripOuterPipes(line);
        var row = new TableRow();

        while (trimmed.Length > 0)
        {
            int pipeIdx = trimmed.IndexOf('|');
            ReadOnlySpan<char> cellContent;
            if (pipeIdx < 0)
            {
                cellContent = trimmed.Trim();
                trimmed = [];
            }
            else
            {
                cellContent = trimmed[..pipeIdx].Trim();
                trimmed = trimmed[(pipeIdx + 1)..];
            }

            var cell = new TableCell();
            if (cellContent.Length > 0)
                cell.Inlines.Append(cellContent.ToString());
            row.Cells.Add(cell);
        }

        return row;
    }

    private static bool CouldBeDelimiterLine(ReadOnlySpan<char> line)
    {
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '|' || c == '-' || c == ':' || char.IsWhiteSpace(c))
                continue;
            return false;
        }
        return true;
    }

    private static void TrimCellTrailingContent(TableCell cell)
    {
        var sb = cell.Inlines.RawBuffer;
        int i = sb.Length - 1;
        while (i >= 0 && char.IsWhiteSpace(sb[i]))
            i--;
        if (i < sb.Length - 1)
            sb.Length = i + 1;
    }

    private static void NormalizeRowCells(TableRow row, int columnCount)
    {
        while (row.Cells.Count < columnCount)
            row.Cells.Add(new TableCell());
        while (row.Cells.Count > columnCount)
            row.Cells.RemoveAt(row.Cells.Count - 1);
    }

    public void Complete(ParserContext context)
    {
        _pendingRow = null;
        _cellIndex = -1;
        _table = null;
    }
}
