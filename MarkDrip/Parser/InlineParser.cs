using System.Diagnostics;
using System.Text;
using MarkDrip.Model;

namespace MarkDrip.Parser;

/// <summary>
/// 流式 Markdown 行内解析器，将原始文本解析为 <see cref="InlineElement"/> 列表。
/// 支持：转义序列、代码范围、强调/粗体、链接、图片、硬/软换行。
/// </summary>
public sealed class InlineParser
{
    private readonly string _text;
    private int _pos;
    private readonly List<InlineElement> _output = new();
    private readonly StringBuilder _textBuf = new();

    // 强调/粗体定界符栈
    private readonly List<Delimiter> _delimiters = new();

    public InlineParser(string text)
    {
        _text = text;
    }

    /// <summary>
    /// 执行解析并返回内联元素列表。
    /// </summary>
    public List<InlineElement> Parse()
    {
        while (_pos < _text.Length)
        {
            var c = _text[_pos];
            switch (c)
            {
                case '\\':
                    ParseEscape();
                    break;
                case '`':
                    ParseCodeSpan();
                    break;
                case '*':
                case '_':
                    ParseDelimiter(c);
                    break;
                case '!':
                    ParseImageOrLink();
                    break;
                case '[':
                    ParseLink();
                    break;
                case '<':
                    ParseAutoLinkOrPassThrough();
                    break;
                case '\n':
                    ParseLineEnd();
                    break;
                case '\r':
                    // 忽略 \r（由 LineBuffer 标准化）
                    _pos++;
                    break;
                default:
                    _textBuf.Append(c);
                    _pos++;
                    break;
            }
        }

        FlushTextBuf();
        ProcessDelimiters();
        return _output;
    }

    // ═══════════════════════════════════════
    //  文本缓冲区
    // ═══════════════════════════════════════

    private void FlushTextBuf()
    {
        if (_textBuf.Length == 0) return;
        _output.Add(new TextRun(_textBuf.ToString()));
        _textBuf.Clear();
    }

    // ═══════════════════════════════════════
    //  转义序列
    // ═══════════════════════════════════════

    /// <summary>
    /// CommonMark 中可被转义的特殊字符集合。
    /// </summary>
    private static readonly bool[] s_escapable = new bool[128];
    static InlineParser()
    {
        var chars = @"\`*_{}[]()#+-.!|~<>""'&";
        foreach (var c in chars)
        {
            if (c < 128) s_escapable[c] = true;
        }
    }

    private void ParseEscape()
    {
        Debug.Assert(_text[_pos] == '\\');
        if (_pos + 1 >= _text.Length)
        {
            // 末尾反斜杠 → 字面量
            _textBuf.Append('\\');
            _pos++;
            return;
        }

        var next = _text[_pos + 1];
        if (next < 128 && s_escapable[next])
        {
            _textBuf.Append(next);
            _pos += 2;
        }
        else if (next == '\n')
        {
            // 硬换行（反斜杠 + 换行）
            FlushTextBuf();
            _output.Add(new LineBreak());
            _pos += 2;
        }
        else
        {
            // 不可转义 → 反斜杠作为字面量
            _textBuf.Append('\\');
            _pos++;
        }
    }

    // ═══════════════════════════════════════
    //  代码范围（`code`）
    // ═══════════════════════════════════════

    private void ParseCodeSpan()
    {
        Debug.Assert(_text[_pos] == '`');

        // 统计开头的反引号个数
        int openTicks = 0;
        while (_pos < _text.Length && _text[_pos] == '`')
        {
            openTicks++;
            _pos++;
        }

        // 查找匹配的闭合反引号序列
        int start = _pos; // 内容开始位置
        int closePos = -1;
        for (int i = start; i < _text.Length; i++)
        {
            if (_text[i] == '`')
            {
                int closeTicks = 0;
                while (i < _text.Length && _text[i] == '`')
                {
                    closeTicks++;
                    i++;
                }

                if (closeTicks == openTicks)
                {
                    closePos = i - closeTicks;
                    _pos = i;
                    break;
                }
                // 反引号个数不匹配，继续搜索
            }
        }

        if (closePos == -1)
        {
            // 没有找到闭合 → 将开头反引号作为普通文本输出，并将内容作为普通文本继续解析
            _textBuf.Append(new string('`', openTicks));
            _textBuf.Append(_text.AsSpan(start));
            _pos = _text.Length;
            FlushTextBuf();
            return;
        }

        FlushTextBuf();

        // 提取代码内容（两端各去掉一个空格如果都有空格）
        var code = _text[start..closePos];
        code = NormalizeCodeContent(code);

        _output.Add(new InlineCode(code));
    }

    private static string NormalizeCodeContent(string code)
    {
        // 如果字符串不为空且两端都有空格，各去掉一个空格
        if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ')
        {
            // 确保不是全部空格
            bool allSpaces = true;
            for (int i = 0; i < code.Length; i++)
            {
                if (code[i] != ' ')
                {
                    allSpaces = false;
                    break;
                }
            }
            if (!allSpaces)
            {
                code = code[1..^1];
            }
        }

        // 将换行符统一为空格
        var sb = new StringBuilder(code.Length);
        for (int i = 0; i < code.Length; i++)
        {
            if (code[i] == '\n')
                sb.Append(' ');
            else
                sb.Append(code[i]);
        }
        return sb.ToString();
    }

    // ═══════════════════════════════════════
    //  强调/粗体定界符（* 和 _）
    // ═══════════════════════════════════════

    private void ParseDelimiter(char ch)
    {
        Debug.Assert(ch is '*' or '_');
        Debug.Assert(_text[_pos] == ch);

        int count = 0;
        int start = _pos;
        while (_pos < _text.Length && _text[_pos] == ch)
        {
            count++;
            _pos++;
        }

        // 判断左右 flanking 状态
        bool leftFlanking = _pos < _text.Length && !char.IsWhiteSpace(_text[_pos]);
        bool rightFlanking = start > 0 && !char.IsWhiteSpace(_text[start - 1]);

        bool canOpen, canClose;
        if (ch == '*')
        {
            canOpen = leftFlanking;
            canClose = rightFlanking;
        }
        else // '_'
        {
            canOpen = leftFlanking && (!rightFlanking || IsPrecededByPunctuation(start));
            canClose = rightFlanking && (!leftFlanking || IsFollowedByPunctuation(_pos));
        }

        // 既不能开也不能关 → 当作普通文本追加到缓冲区，不拆分配置
        if (!canOpen && !canClose)
        {
            _textBuf.Append(new string(ch, count));
            return;
        }

        // 需要创建定界符：先刷新之前的文本缓冲区
        FlushTextBuf();

        var delim = new Delimiter
        {
            Char = ch,
            Count = count,
            OutputIndex = _output.Count,
            CanOpen = canOpen,
            CanClose = canClose,
            StartPos = start
        };

        _delimiters.Add(delim);

        // 在输出中放入占位符元素（后续 ProcessDelimiters 会替换）
        _output.Add(new TextRun(new string(ch, count)));
    }

    private bool IsPrecededByPunctuation(int index)
    {
        if (index <= 0) return false;
        var c = _text[index - 1];
        return char.IsPunctuation(c) || IsUnicodePunctuation(c);
    }

    private bool IsFollowedByPunctuation(int index)
    {
        if (index >= _text.Length) return false;
        var c = _text[index];
        return char.IsPunctuation(c) || IsUnicodePunctuation(c);
    }

    private static bool IsUnicodePunctuation(char c)
    {
        // ASCII 标点由 char.IsPunctuation 覆盖
        // 额外处理 Unicode 类别
        return char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.ConnectorPunctuation ||
               char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.DashPunctuation ||
               char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.ClosePunctuation ||
               char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OpenPunctuation ||
               char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.InitialQuotePunctuation ||
               char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.FinalQuotePunctuation ||
               char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherPunctuation;
    }

    /// <summary>
    /// 处理定界符栈，匹配开闭定界符并创建 Emphasis/StrongEmphasis。
    /// </summary>
    private void ProcessDelimiters()
    {
        // 从右到左扫描定界符，寻找闭合定界符
        for (int i = 0; i < _delimiters.Count; i++)
        {
            var closer = _delimiters[i];
            if (!closer.CanClose) continue;

            // 从右到左搜索匹配的开启定界符
            for (int j = i - 1; j >= 0; j--)
            {
                var opener = _delimiters[j];
                if (!opener.CanOpen) continue;
                if (opener.Char != closer.Char) continue;

                // 找到了匹配
                // 确定粗体还是斜体
                int usedOpener = Math.Min(opener.Count, closer.Count);
                int openerRemain = opener.Count - usedOpener;
                int closerRemain = closer.Count - usedOpener;

                // 收集 opening 和 closing 之间的内容
                int openIdx = opener.OutputIndex;
                int closeIdx = closer.OutputIndex;

                // 更新 opener 和 closer 的剩余计数
                opener.Count = openerRemain;
                closer.Count = closerRemain;

                // 用 Emphasis/StrongEmphasis 替换内容
                int elemCount = closeIdx - openIdx + 1;
                var children = new List<InlineElement>();
                for (int k = openIdx + 1; k < closeIdx; k++)
                {
                    children.Add(_output[k]);
                }

                // 创建嵌套结构
                InlineElement wrapped;
                if (usedOpener >= 2)
                {
                    // 粗体 **
                    var strong = new StrongEmphasis(children.AsReadOnly());
                    // 如果还有剩余的 *，在粗体外层再包斜体
                    if (openerRemain >= 1 && closerRemain >= 1)
                    {
                        // **text* → 实际上是 * **text * → <em><strong>text</strong></em>
                        // 这种情况比较复杂，简化处理
                        wrapped = strong;
                        // 更新剩余的 counts
                        opener.Count = openerRemain - 1;
                        closer.Count = closerRemain - 1;
                        // 在外部再包一层 Emphasis
                        wrapped = new Emphasis(new List<InlineElement> { wrapped }.AsReadOnly());
                    }
                    else
                    {
                        wrapped = strong;
                    }
                }
                else
                {
                    // 斜体 *
                    wrapped = new Emphasis(children.AsReadOnly());
                }

                // 替换输出
                _output.RemoveRange(openIdx, elemCount);
                _output.Insert(openIdx, wrapped);

                // 更新后续定界符的 OutputIndex
                int delta = 1 - elemCount;
                for (int k = i + 1; k < _delimiters.Count; k++)
                {
                    _delimiters[k].OutputIndex += delta;
                }

                // 如果 closer 还有剩余，保留它
                if (closer.Count > 0)
                {
                    _delimiters[i].OutputIndex = openIdx + 1;
                    // Insert remaining closer as text
                    _output.Insert(openIdx + 1, new TextRun(new string(closer.Char, closer.Count)));
                    // 更新后续 OutputIndex
                    for (int k = i + 1; k < _delimiters.Count; k++)
                    {
                        _delimiters[k].OutputIndex += 1;
                    }
                }
                else
                {
                    // 移除已处理的定界符
                    _delimiters.RemoveAt(i);
                    i--;
                }

                // 如果 opener 没有剩余，移除；否则保留
                if (opener.Count <= 0)
                {
                    _delimiters.RemoveAt(j);
                    i--;
                }

                break; // 处理下一个 closer
            }
        }

        // 将未匹配的定界符转换为文本
        for (int i = 0; i < _delimiters.Count; i++)
        {
            var d = _delimiters[i];
            if (d.Count > 0)
            {
                // 替换占位文本为实际文本
                if (d.OutputIndex < _output.Count && _output[d.OutputIndex] is TextRun)
                {
                    _output[d.OutputIndex] = new TextRun(new string(d.Char, d.Count));
                }
            }
        }
    }

    /// <summary>
    /// 定界符运行时状态。
    /// </summary>
    private sealed class Delimiter
    {
        public char Char { get; set; }
        public int Count { get; set; }
        public int OutputIndex { get; set; }
        public bool CanOpen { get; set; }
        public bool CanClose { get; set; }
        public int StartPos { get; set; }
    }

    // ═══════════════════════════════════════
    //  图片和链接
    // ═══════════════════════════════════════

    private void ParseImageOrLink()
    {
        Debug.Assert(_text[_pos] == '!');

        if (_pos + 1 < _text.Length && _text[_pos + 1] == '[')
        {
            // 图片 ![alt](url)
            _pos += 2; // 跳过 "!["
            var alt = CollectLinkText();
            if (alt is null)
            {
                // 未闭合 → 回退输出 "!["
                FlushTextBuf();
                _output.Add(new TextRun("!["));
                return;
            }

            if (_pos < _text.Length && _text[_pos] == '(')
            {
                _pos++; // 跳过 '('
                var (url, title) = ParseLinkDestination();
                if (url is not null)
                {
                    FlushTextBuf();
                    _output.Add(new Image(url, alt));
                    return;
                }
            }

            // 没有有效链接 → 回退输出 "![" + alt + "]"
            FlushTextBuf();
            _output.Add(new TextRun("![" + alt + "]"));
        }
        else
        {
            _textBuf.Append('!');
            _pos++;
        }
    }

    private void ParseLink()
    {
        Debug.Assert(_text[_pos] == '[');

        _pos++; // 跳过 '['
        var text = CollectLinkText();
        if (text is null)
        {
            // 未闭合
            _textBuf.Append('[');
            return;
        }

        if (_pos < _text.Length && _text[_pos] == '(')
        {
            _pos++; // 跳过 '('
            var (url, title) = ParseLinkDestination();
            if (url is not null)
            {
                // 解析链接文本的内联元素
                var linkTextParser = new InlineParser(text);
                var linkChildren = linkTextParser.Parse();

                FlushTextBuf();
                _output.Add(new Link(url, title, linkChildren.AsReadOnly()));
                return;
            }
        }

        // 尝试简写引用链接 [text][] 或 [text]
        // Phase 3 MVP 暂不支持引用链接 → 回退输出原始文本
        FlushTextBuf();
        _output.Add(new TextRun("[" + text + "]"));
    }

    /// <summary>
    /// 收集 '[' 和 ']' 之间的链接文本。返回收集到的文本，若未闭合返回 null。
    /// </summary>
    private string? CollectLinkText()
    {
        int depth = 1;
        int start = _pos;
        while (_pos < _text.Length)
        {
            if (_text[_pos] == '\\' && _pos + 1 < _text.Length)
            {
                _pos += 2;
                continue;
            }
            if (_text[_pos] == '[')
            {
                depth++;
            }
            else if (_text[_pos] == ']')
            {
                depth--;
                if (depth == 0)
                {
                    var result = _text[start.._pos];
                    _pos++; // 跳过 ']'
                    return result;
                }
            }
            _pos++;
        }
        return null; // 未闭合
    }

    /// <summary>
    /// 解析链接目标地址和可选标题。
    /// 返回 (url, title)，解析失败时 url 为 null。
    /// </summary>
    private (string? Url, string? Title) ParseLinkDestination()
    {
        SkipWhitespace();

        if (_pos >= _text.Length) return (null, null);

        string? url;
        if (_text[_pos] == '<')
        {
            // 尖括号内地址
            _pos++;
            int start = _pos;
            while (_pos < _text.Length && _text[_pos] != '>' && _text[_pos] != '\n')
                _pos++;
            if (_pos >= _text.Length || _text[_pos] != '>')
                return (null, null);
            url = _text[start.._pos];
            _pos++; // 跳过 '>'
        }
        else
        {
            // 普通地址（不含空格、控制字符、闭括号）
            int parenDepth = 0;
            int start = _pos;
            while (_pos < _text.Length)
            {
                var c = _text[_pos];
                if (c == '\\' && _pos + 1 < _text.Length)
                {
                    _pos += 2;
                    continue;
                }
                if (c == '(')
                {
                    parenDepth++;
                }
                else if (c == ')')
                {
                    if (parenDepth == 0) break;
                    parenDepth--;
                }
                else if (c == ' ' || c == '\n')
                {
                    break;
                }
                _pos++;
            }
            url = _text[start.._pos];
            if (url.Length == 0) return (null, null);
        }

        // 可选标题
        SkipWhitespace();
        string? title = null;
        if (_pos < _text.Length && (_text[_pos] == '"' || _text[_pos] == '\''))
        {
            var quote = _text[_pos];
            _pos++;
            int start = _pos;
            while (_pos < _text.Length && _text[_pos] != quote && _text[_pos] != '\n')
                _pos++;
            if (_pos < _text.Length && _text[_pos] == quote)
            {
                title = _text[start.._pos];
                _pos++; // 跳过引号
                SkipWhitespace();
            }
        }

        // 期待 ')'
        if (_pos < _text.Length && _text[_pos] == ')')
        {
            _pos++;
            return (url, title);
        }

        return (null, null);
    }

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && (_text[_pos] == ' ' || _text[_pos] == '\n'))
            _pos++;
    }

    // ═══════════════════════════════════════
    //  自动链接 / HTML 标签
    // ═══════════════════════════════════════

    private void ParseAutoLinkOrPassThrough()
    {
        Debug.Assert(_text[_pos] == '<');

        // 自动链接：<url> 或 <email>
        // Phase 3 MVP 暂不完整支持，直接当作文本
        _textBuf.Append('<');
        _pos++;
    }

    // ═══════════════════════════════════════
    //  行尾
    // ═══════════════════════════════════════

    private void ParseLineEnd()
    {
        Debug.Assert(_text[_pos] == '\n');

        // 检查前面两个字符是否为空格（硬换行：两个空格 + 换行）
        // _pos 是 \n 的位置，前面两个字符在 _pos-2 和 _pos-1
        bool hardBreak = _pos >= 2 && _text[_pos - 1] == ' ' && _text[_pos - 2] == ' ';

        FlushTextBuf();

        if (hardBreak)
        {
            // 去掉已经追加的两个空格（在文本缓冲区中）
            // 由于空格已经被追加到 _textBuf 中，然后 FlushTextBuf 已经 Flush 了
            // 所以我们需要处理输出中的最后一个 TextRun
            if (_output.Count > 0 && _output[^1] is TextRun last && last.Text.EndsWith("  "))
            {
                _output[^1] = new TextRun(last.Text[..^2]);
            }
            _output.Add(new LineBreak());
        }
        else
        {
            _output.Add(new SoftLineBreak());
        }

        _pos++;
    }
}
