using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class StreamParser
{
    private readonly List<IBlockParser> _blockParsers = null!;
    private readonly ParagraphParser paragraphParser = null!;
    private readonly StringBuilder _pendingBuffer = new();
    private readonly LineBuffer _currentLine = new();
    private ParserContext context;
    private IBlockParser? currentParser;

    public ObservableCollection<DocumentBlock> Blocks => context.Blocks;

    public StreamParser() : this(new())
    {
    }

    public StreamParser(ObservableCollection<DocumentBlock> targetBlocks)
    {
        context = new ParserContext(targetBlocks, new LineBufferView(_currentLine));

        _blockParsers = [
            new AtxHeaderParser(),
            new ThematicBreakParser(),
            new TableParser(),
            new SetextHeaderParser(),
            new BlockQuoteParser(),
            new CodeBlockParser(),
            new ListParser(),
            new IndentedCodeBlockParser(),
        ];
        paragraphParser = new ParagraphParser();
    }

    internal void FeedSample()
    {
        var text = """
            # 列表渲染测试

            ## 1. 基本无序列表

            - 苹果
            - 香蕉
            - 樱桃

            ## 2. 基本有序列表

            1. 第一步
            2. 第二步
            3. 第三步
            """;
        foreach (var item in text)
            Feed(item.ToString());
    }

    public void Feed(ReadOnlySpan<char> chunk)
    {
        while (chunk.Length > 0)
        {
            var index = chunk.IndexOfAny(new[] { '\n', '\r' });
            var segment = index == -1 ? chunk : chunk[..(index + 1)];

            if (currentParser != null)
            {
                int lineLenBeforeAppend = _currentLine.Length;
                _currentLine.Append(segment);

                var result_ = currentParser.Append(segment, context);
                if (result_ == AppendResult.NeedMatch)
                    currentParser = null;
                else if (result_ == AppendResult.YieldLine)
                {
                    currentParser = null;
                    if (lineLenBeforeAppend > 0)
                        _pendingBuffer.Append(_currentLine.Slice(0, lineLenBeforeAppend));
                    _currentLine.Truncate(lineLenBeforeAppend);
                    continue;
                }
            }
            else
            {
                _pendingBuffer.Append(segment);
                _currentLine.Append(segment);

                var hadBufferedContent = _pendingBuffer.Length > segment.Length;
                ReadOnlySpan<char> lineStr = hadBufferedContent
                    ? _pendingBuffer.ToString().AsSpan()
                    : segment;

                var matchResults = new (IBlockParser, MatchResult)[_blockParsers.Count];
                for (int i = 0; i < _blockParsers.Count; i++)
                    matchResults[i] = (_blockParsers[i], _blockParsers[i].TryMatch(lineStr, context));

                bool hasPartialMatch = matchResults.Any(m => m.Item2 == MatchResult.PartialMatch);
                bool hasFullMatch = matchResults.Any(m => m.Item2 == MatchResult.FullMatch);
                bool allNoMatch = !hasFullMatch && !hasPartialMatch;

                if (hasPartialMatch)
                {
                }
                else if (hasFullMatch)
                {
                    _pendingBuffer.Clear();
                    var parser = matchResults.First(m => m.Item2 == MatchResult.FullMatch).Item1;
                    parser.OnMatch(lineStr, context);
                    currentParser = parser.Append(lineStr, context) == AppendResult.NeedMatch ? null : parser;
                }
                else if (allNoMatch)
                {
                    _pendingBuffer.Clear();
                    paragraphParser.OnMatch(lineStr, context);
                    currentParser = paragraphParser.Append(lineStr, context) == AppendResult.NeedMatch ? null : paragraphParser;
                }
            }

            if (index >= 0)
                _currentLine.Clear();

            chunk = chunk[(index == -1 ? chunk.Length : index + 1)..];
        }
    }

    public void Complete()
    {
        if (_pendingBuffer.Length > 0)
        {
            var text = _pendingBuffer.ToString();
            _pendingBuffer.Clear();
            var para = new ParagraphBlock();
            para.Inlines.Append(text);
            context.Blocks.Add(para);
        }

        currentParser?.Complete(context);
        currentParser = null;

        FinalizeBlocks(context.Blocks);
    }

    private static void FinalizeBlocks(ObservableCollection<DocumentBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block.Status == BlockStatus.Open)
                block.Status = BlockStatus.Finalized;

            switch (block)
            {
                case ParagraphBlock p:
                    p.Inlines.Seal();
                    break;
                case HeadingBlock h:
                    h.Inlines.Seal();
                    break;
                case BlockQuoteBlock q:
                    FinalizeBlocks(q.Children);
                    break;
                case ListItemBlock li:
                    FinalizeBlocks(li.Children);
                    break;
                case ListBlock list:
                    foreach (var item in list.Items)
                    {
                        if (item.Status == BlockStatus.Open)
                            item.Status = BlockStatus.Finalized;
                        FinalizeBlocks(item.Children);
                    }
                    break;
                case TableBlock t:
                    foreach (var row in t.Rows)
                        foreach (var cell in row.Cells)
                            cell.Inlines.Seal();
                    break;
            }
        }
    }
}
