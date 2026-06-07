using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class StreamParser : IBlockParser
{
    private readonly List<IBlockParser> _blockParsers = null!;
    private readonly ParagraphParser paragraphParser = null!;
    /// <summary>
    /// 缓存最后未处理的块
    /// </summary>
    private readonly StringBuilder _pendingBuffer = new();
    /// <summary>
    /// 缓存最后未解析完成的行
    /// </summary>
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

            _currentLine.Append(segment);
            _pendingBuffer.Append(segment);

            // 构造chuck
            // 先看当前是否有缓冲的内容，如果有的话就把缓冲的内容和当前的segment一起构造成一个新的字符串，否则就直接用当前的segment
            if (_pendingBuffer.Length > segment.Length)
                segment = _pendingBuffer.ToString().AsSpan();

            bool isLineEnd = segment.EndsWith("\n") || segment.EndsWith("\r");

            var chuck = new TextChunk(segment, new LineBufferView(_currentLine));
            var result = this.Append(chuck, context);

            if (result == AppendResult.ReMatch)
            {
                // 当前解析器要求重新匹配当前行，那我们就重新匹配一次。
                // 注意解析器可能吃掉了一些内容，所以我们需要将当前整行的内容都重新匹配，而不是只匹配当前这一小块
                currentParser = null;
                var line = new TextChunk(_currentLine.Slice(0, _currentLine.Length), new LineBufferView(_currentLine));
                result = this.Append(line, context);
            }

            // 每个判断都写一次，用冗余换取清晰，虽然有点啰嗦
            if (result == AppendResult.KeepFeeding)
            {
                // 解析器要求继续喂数据，说明已经接受了当前块，所以干净的等下一块到达就可以了
                _pendingBuffer.Clear();
            }
            else if (result == AppendResult.NeedNextChunk)
            {
                // 解析器需要更多块来处理当前行内容，说明还不够，这里什么都不用做
                // _pendingBuffer 和 _currentLine 继续累积内容，等待下一块到来
            }
            else if (result == AppendResult.NextLineNeedMatch)
            {
                // 解析器需要重新匹配，说明下一行有歧义或者可以被高优先级匹配，此时应该是行尾，所以把当前行的内容清空，并且把当前的解析器置空，等待下一行的内容来重新匹配
                _pendingBuffer.Clear();
                currentParser = null;
            }

            // 当前行处理完毕
            if (isLineEnd)
                _currentLine.Clear();

            // 如果还有剩余内容，继续处理
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

    public MatchResult TryMatch(TextChunk chunk, ParserContext context)
    {
        var matchResults = new (IBlockParser, MatchResult)[_blockParsers.Count];
        for (int i = 0; i < _blockParsers.Count; i++)
            matchResults[i] = (_blockParsers[i], _blockParsers[i].TryMatch(chunk, context));

        bool hasPartialMatch = matchResults.Any(m => m.Item2 == MatchResult.PartialMatch);
        bool hasFullMatch = matchResults.Any(m => m.Item2 == MatchResult.FullMatch);
        bool allNoMatch = !hasFullMatch && !hasPartialMatch;

        if (hasPartialMatch)
        {
            return MatchResult.PartialMatch;
        }
        else if (hasFullMatch)
        {
            currentParser = matchResults.First(m => m.Item2 == MatchResult.FullMatch).Item1;
            currentParser.OnMatch(chunk.Text, context);
            return MatchResult.FullMatch;
        }

        currentParser = paragraphParser;
        return MatchResult.FullMatch;

    }

    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        // 不需要
    }

    public AppendResult Append(TextChunk chuck, ParserContext context)
    {
        // 如果当前没有匹配的解析器，尝试匹配一次
        if (currentParser == null)
        {
            var matchResult = TryMatch(chuck, context);
            if (matchResult == MatchResult.PartialMatch)
            {
                // 说明需要更多输入
                return AppendResult.NeedNextChunk;
            }

            // 理论上这判断也可以让调用方来写，但这样也挺好的
        }

        // 走到这里肯定是有匹配的解析器了，继续追加内容
        return currentParser!.Append(chuck, context);
    }
}
