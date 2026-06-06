using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

class IndentedCodeBlockParser : IBlockParser
{
    private int _blankLineCount;
    private bool _initialContentProcessed;

    public MatchResult TryMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        // 不能打断段落（paragraph.interrupting）
        if (context.PreviousBlock is ParagraphBlock { Status: BlockStatus.Open })
            return MatchResult.NoMatch;

        int spaces = TextUtils.CountLeadingSpaces(line);
        if (spaces < 4) return MatchResult.NoMatch;

        // 4 空格后必须有非空白内容（纯空白行不能启动缩进代码块）
        ReadOnlySpan<char> afterIndent = line[4..];
        if (afterIndent.IsEmpty) return MatchResult.NoMatch;
        var trimmed = TextUtils.StripTrailingNewline(afterIndent);
        if (trimmed.IsEmpty) return MatchResult.NoMatch;
        for (int i = 0; i < trimmed.Length; i++)
            if (!char.IsWhiteSpace(trimmed[i]))
                return MatchResult.FullMatch;

        return MatchResult.NoMatch;
    }

    public void OnMatch(ReadOnlySpan<char> line, ParserContext context)
    {
        var code = new CodeBlock();
        context.Blocks.Add(code);
        _blankLineCount = 0;
        _initialContentProcessed = false;

        // 剥去前 4 空格，将首行内容写入代码块
        AppendLineContent(code, line);
    }

    public AppendResult Append(ReadOnlySpan<char> chunk, ParserContext context)
    {
        if (context.PreviousBlock is not CodeBlock code)
            return AppendResult.NeedMatch;

        // OnMatch 已处理首行，跳过重复处理
        if (!_initialContentProcessed)
        {
            _initialContentProcessed = true;
            return AppendResult.KeepFeeding;
        }

        // ── Blank line → buffer ──
        if (TextUtils.IsBlankLine(chunk))
        {
            _blankLineCount++;
            return AppendResult.KeepFeeding;
        }

        // ── 有悬而未决的空白行 ──
        if (_blankLineCount > 0)
        {
            if (TextUtils.CountLeadingSpaces(chunk) >= 4)
            {
                // 续行：空白属于块内 chunk 分隔，flush 到内容中
                FlushBlankLines(code);
                AppendLineContent(code, chunk);
                _blankLineCount = 0;
                return AppendResult.KeepFeeding;
            }

            // 非缩进行 → 代码块结束，空白不纳入内容
            _blankLineCount = 0;
            code.Status = BlockStatus.Finalized;
            return AppendResult.YieldLine;
        }

        // ── 缩进的内容行 ──
        if (TextUtils.CountLeadingSpaces(chunk) >= 4)
        {
            AppendLineContent(code, chunk);
            return AppendResult.KeepFeeding;
        }

        // ── 非缩进行 → 代码块结束 ──
        code.Status = BlockStatus.Finalized;
        return AppendResult.YieldLine;
    }

    private void FlushBlankLines(CodeBlock code)
    {
        for (int i = 0; i < _blankLineCount; i++)
        {
            code.Content.Append('\n');
            code.NotifyContentChanged();
        }
    }

    /// <summary>剥去前 4 空格和尾部换行，将剩余内容追加到代码块。</summary>
    private static void AppendLineContent(CodeBlock code, ReadOnlySpan<char> chunk)
    {
        var content = TextUtils.StripTrailingNewline(chunk);
        if (content.Length > 4)
        {
            if (code.Content.Length > 0)
                code.Content.Append('\n');
            code.Content.Append(content[4..]);
            code.NotifyContentChanged();
        }
        else
        {
            if (code.Content.Length > 0)
                code.Content.Append('\n');
            code.NotifyContentChanged();
        }
    }
}
