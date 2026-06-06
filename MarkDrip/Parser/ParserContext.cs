using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class ParserContext
{
    public ObservableCollection<DocumentBlock> Blocks { get; }

    /// <summary>
    /// 当前行的只读视图（由 StreamParser.Feed 统一写入 _currentLine，此处仅暴露 Slice）。
    /// </summary>
    public LineBufferView LastLineBuffer { get; }

    /// <summary>
    /// 前一个块（如果有）。解析器可以使用此信息来决定当前行是否属于前一个块的一部分。
    /// </summary>
    public DocumentBlock? PreviousBlock { get => Blocks.LastOrDefault(); }

    /// <summary>创建空上下文（LastLineBuffer 为空的哑实例，用于只做 TryMatch 的场景）。</summary>
    public ParserContext()
    {
        Blocks = new ObservableCollection<DocumentBlock>();
        LastLineBuffer = new LineBufferView(new LineBuffer());
    }

    /// <summary>使用指定的 LastLineBuffer 只读视图创建上下文。</summary>
    public ParserContext(LineBufferView lastLineBuffer)
    {
        Blocks = new ObservableCollection<DocumentBlock>();
        LastLineBuffer = lastLineBuffer;
    }

    /// <summary>
    /// 供内部解析器使用（如 BlockQuoteParser 的子解析器指向 BlockQuoteBlock.Children）。
    /// </summary>
    public ParserContext(ObservableCollection<DocumentBlock> targetBlocks, LineBufferView lastLineBuffer)
    {
        Blocks = targetBlocks;
        LastLineBuffer = lastLineBuffer;
    }
}
