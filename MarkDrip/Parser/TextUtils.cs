using MarkDrip.Model;
using System.Collections.ObjectModel;
using System.Text;

namespace MarkDrip.Parser;

internal static class TextUtils
{
    public static ReadOnlySpan<char> StripTrailingNewline(ReadOnlySpan<char> s)
    {
        if (s.Length == 0) return s;
        if (s[^1] == '\n') return s[..^1];
        if (s[^1] == '\r') return s[..^1];
        return s;
    }

    public static ReadOnlySpan<char> StripAllTrailingNewlines(ReadOnlySpan<char> line)
    {
        var s = line;
        while (s.Length > 0 && (s[^1] is '\n' or '\r'))
            s = s[..^1];
        return s;
    }

    public static bool IsBlankLine(ReadOnlySpan<char> line)
    {
        var s = StripAllTrailingNewlines(line);
        return s.IsEmpty || s.IsWhiteSpace();
    }

    public static bool IsWhitespaceOnly(ReadOnlySpan<char> s)
    {
        for (int i = 0; i < s.Length; i++)
            if (!char.IsWhiteSpace(s[i])) return false;
        return true;
    }

    public static bool IsNotWhiteSpace(this ReadOnlySpan<char> s)
    {
        return !s.IsWhiteSpace();
    }

    /// <summary>统计前导空格数，最多 max 个（CommonMark 通用缩进上限为 3）。</summary>
    public static int LeadingSpaces(ReadOnlySpan<char> s, int max = 3)
    {
        int count = 0;
        while (count < s.Length && count < max && s[count] == ' ')
            count++;
        return count;
    }

    public static int CountLeadingSpaces(ReadOnlySpan<char> s)
    {
        int count = 0;
        while (count < s.Length && s[count] == ' ')
            count++;
        return count;
    }
}


