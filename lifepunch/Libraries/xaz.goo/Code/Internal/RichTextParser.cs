using System;
using System.Text;
using Sandbox;

namespace Goo.Internal;

internal static class TextSourceResolver
{
    internal static string Read(string? content, string path, BaseFileSystem fileSystem)
    {
        if (!string.IsNullOrEmpty(content))
            throw new InvalidOperationException("Text cannot set both Content and Path.");
        return fileSystem.ReadAllText(path) ?? string.Empty;
    }
}

/// <summary>Streams Goo's Markdown subset into the inline HTML understood by Sandbox.UI.Label.</summary>
internal static class RichTextParser
{
    const string Strong = "font-weight: 700;";
    const string Emphasis = "font-style: italic;";
    const string Strike = "text-decoration-line: line-through;";
    const string Link = "text-decoration-line: underline;";
    const string Code = "font-family: Roboto Mono; background-color: rgba(0, 0, 0, 0.25);";

    internal static string Parse(string? source)
    {
        var text = (source ?? string.Empty).AsSpan();
        var output = new StringBuilder(text.Length + 32).Append("<span>");
        var emitted = false;
        var inCode = false;
        var codeOpen = false;
        var codeLines = 0;
        char fence = default;
        var fenceLength = 0;

        var start = 0;
        while (start <= text.Length)
        {
            var end = start;
            while (end < text.Length && text[end] is not ('\r' or '\n')) end++;
            var line = text[start..end];

            if (inCode)
            {
                if (IsFenceClose(line, fence, fenceLength))
                {
                    OpenCode(output, ref emitted, ref codeOpen);
                    output.Append("</span>");
                    inCode = codeOpen = false;
                    codeLines = fenceLength = 0;
                    fence = default;
                }
                else
                {
                    OpenCode(output, ref emitted, ref codeOpen);
                    if (codeLines++ > 0) output.Append("<br>");
                    Encode(output, line);
                }
            }
            else if (TryFenceOpen(line, out fence, out fenceLength))
            {
                inCode = true;
                codeLines = 0;
            }
            else
            {
                BeginItem(output, ref emitted);
                AppendLine(output, line);
            }

            if (end == text.Length) break;
            start = end + 1;
            if (text[end] == '\r' && start < text.Length && text[start] == '\n') start++;
        }

        if (inCode)
        {
            OpenCode(output, ref emitted, ref codeOpen);
            output.Append("</span>");
        }
        return output.Append("</span>").ToString();
    }

    static void BeginItem(StringBuilder output, ref bool emitted)
    {
        if (emitted) output.Append("<br>");
        emitted = true;
    }

    static void OpenCode(StringBuilder output, ref bool emitted, ref bool open)
    {
        if (open) return;
        BeginItem(output, ref emitted);
        output.Append("<span style=\"").Append(Code).Append("\">");
        open = true;
    }

    static void AppendLine(StringBuilder output, ReadOnlySpan<char> line)
    {
        if (line.IsEmpty) return;
        var offset = LeadingSpaces(line, 3);
        var content = line[offset..];
        var heading = 0;
        while (heading < content.Length && heading < 6 && content[heading] == '#') heading++;

        if (heading > 0 && heading < content.Length && content[heading] == ' ')
        {
            OpenStyle(output, Strong);
            AppendInline(output, content[(heading + 1)..].TrimEnd(), 0);
            output.Append("</span>");
        }
        else if (content.Length >= 2 && IsBullet(content[0]) && content[1] == ' ')
        {
            Indent(output, offset);
            output.Append("&#8226; ");
            AppendInline(output, content[2..], 0);
        }
        else if (TryOrderedMarker(content, out var markerLength))
        {
            Indent(output, offset);
            Encode(output, content[..markerLength]);
            output.Append(' ');
            AppendInline(output, content[(markerLength + 1)..], 0);
        }
        else if (content.Length >= 2 && content[0] == '>' && content[1] == ' ')
        {
            Indent(output, offset);
            output.Append("&#8250; ");
            AppendInline(output, content[2..], 0);
        }
        else AppendInline(output, line, 0);
    }

    static void AppendInline(StringBuilder output, ReadOnlySpan<char> source, int depth)
    {
        if (depth >= 32)
        {
            Encode(output, source);
            return;
        }

        var htmlExhausted = false;
        var linksExhausted = false;
        var codeExhausted = false;
        var strongStarExhausted = false;
        var strongUnderscoreExhausted = false;
        var strikeExhausted = false;
        var emphasisStarExhausted = false;
        var emphasisUnderscoreExhausted = false;
        for (var i = 0; i < source.Length;)
        {
            if (!IsInlineSpecial(source[i]))
            {
                var end = i + 1;
                while (end < source.Length && !IsInlineSpecial(source[end])) end++;
                output.Append(source[i..end]);
                i = end;
                continue;
            }
            if (source[i] == '\\' && i + 1 < source.Length && IsEscapable(source[i + 1]))
            {
                Encode(output, source[i + 1]);
                i += 2;
                continue;
            }
            if (source[i] == '&' && TryEntity(output, source, i, out var consumed))
            {
                i += consumed;
                continue;
            }
            if (source[i] == '<' && !htmlExhausted)
            {
                if (source[(i + 1)..].IndexOf('>') < 0) htmlExhausted = true;
                else if (TryAutolink(output, source, i, out consumed) || TryHtml(output, source, i, out consumed))
                {
                    i += consumed;
                    continue;
                }
            }
            if (source[i] == '`' && !codeExhausted)
            {
                if (source[(i + 1)..].IndexOf('`') < 0) codeExhausted = true;
                else if (TryCode(output, source, i, out consumed))
                {
                    i += consumed;
                    continue;
                }
            }
            if (source[i] == '[' && !linksExhausted)
            {
                if (source[(i + 1)..].IndexOf(']') < 0) linksExhausted = true;
                else if (TryLink(output, source, i, out consumed))
                {
                    i += consumed;
                    continue;
                }
            }
            if (TryAvailableDelimiter(output, source, i, "**", Strong, depth, ref strongStarExhausted, out consumed)
                || TryAvailableDelimiter(output, source, i, "__", Strong, depth, ref strongUnderscoreExhausted, out consumed)
                || TryAvailableDelimiter(output, source, i, "~~", Strike, depth, ref strikeExhausted, out consumed)
                || TryAvailableDelimiter(output, source, i, "*", Emphasis, depth, ref emphasisStarExhausted, out consumed)
                || TryAvailableDelimiter(output, source, i, "_", Emphasis, depth, ref emphasisUnderscoreExhausted, out consumed))
            {
                i += consumed;
                continue;
            }
            Encode(output, source[i++]);
        }
    }

    static bool TryAvailableDelimiter(StringBuilder output, ReadOnlySpan<char> source, int start, ReadOnlySpan<char> delimiter, string style, int depth, ref bool exhausted, out int consumed)
    {
        consumed = 0;
        if (exhausted) return false;
        var matched = TryDelimited(output, source, start, delimiter, style, depth, out consumed, out var noCloser);
        exhausted |= noCloser;
        return matched;
    }

    static bool TryDelimited(StringBuilder output, ReadOnlySpan<char> source, int start, ReadOnlySpan<char> delimiter, string style, int depth, out int consumed, out bool noCloser)
    {
        consumed = 0;
        noCloser = false;
        if (!At(source, start, delimiter) || !CanOpen(source, start, delimiter)) return false;
        var close = FindDelimiter(source, start + delimiter.Length, delimiter);
        if (close < 0)
        {
            noCloser = true;
            return false;
        }
        OpenStyle(output, style);
        AppendInline(output, source[(start + delimiter.Length)..close], depth + 1);
        output.Append("</span>");
        consumed = close + delimiter.Length - start;
        return true;
    }

    static bool TryCode(StringBuilder output, ReadOnlySpan<char> source, int start, out int consumed)
    {
        consumed = 0;
        var run = 1;
        while (start + run < source.Length && source[start + run] == '`') run++;
        var close = FindBackticks(source, start + run, run);
        if (close < 0) return false;
        var code = source[(start + run)..close];
        if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && !code.Trim().IsEmpty) code = code[1..^1];
        OpenStyle(output, Code);
        Encode(output, code);
        output.Append("</span>");
        consumed = close + run - start;
        return true;
    }

    static bool TryLink(StringBuilder output, ReadOnlySpan<char> source, int start, out int consumed)
    {
        consumed = 0;
        var labelEnd = FindBalanced(source, start + 1, '[', ']');
        if (labelEnd < 0 || labelEnd + 1 >= source.Length || source[labelEnd + 1] != '(') return false;
        var destinationEnd = FindBalanced(source, labelEnd + 2, '(', ')');
        if (destinationEnd < 0) return false;
        var destination = Destination(source[(labelEnd + 2)..destinationEnd]);
        if (destination.IsEmpty) return false;
        output.Append("<a href=\"");
        EncodeAttribute(output, destination);
        output.Append("\" style=\"").Append(Link).Append("\">");
        Encode(output, source[(start + 1)..labelEnd]);
        output.Append("</a>");
        consumed = destinationEnd + 1 - start;
        return true;
    }

    static bool TryAutolink(StringBuilder output, ReadOnlySpan<char> source, int start, out int consumed)
    {
        consumed = 0;
        var relativeClose = source[(start + 1)..].IndexOf('>');
        if (relativeClose < 0) return false;
        var close = start + 1 + relativeClose;
        var target = source[(start + 1)..close];
        if ((!target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) || HasWhitespace(target)) return false;
        output.Append("<a href=\"");
        EncodeAttribute(output, target);
        output.Append("\" style=\"").Append(Link).Append("\">");
        Encode(output, target);
        output.Append("</a>");
        consumed = close + 1 - start;
        return true;
    }

    static bool TryHtml(StringBuilder output, ReadOnlySpan<char> source, int start, out int consumed)
    {
        consumed = 0;
        if (start + 1 >= source.Length) return false;
        var lead = source[start + 1];
        if (!char.IsLetter(lead) && lead is not ('/' or '!' or '?')) return false;
        char quote = default;
        for (var i = start + 2; i < source.Length; i++)
        {
            var c = source[i];
            if (quote != default)
            {
                if (c == quote) quote = default;
            }
            else if (c is '\'' or '"') quote = c;
            else if (c == '>')
            {
                consumed = i + 1 - start;
                output.Append(source.Slice(start, consumed));
                return true;
            }
        }
        return false;
    }

    static bool TryEntity(StringBuilder output, ReadOnlySpan<char> source, int start, out int consumed)
    {
        consumed = 0;
        var limit = Math.Min(source.Length, start + 18);
        var relative = source.Slice(start + 1, limit - start - 1).IndexOf(';');
        if (relative <= 0) return false;
        var end = start + 1 + relative;
        for (var i = start + 1; i < end; i++)
            if (!char.IsLetterOrDigit(source[i]) && source[i] != '#') return false;
        consumed = end + 1 - start;
        output.Append(source.Slice(start, consumed));
        return true;
    }

    static int FindDelimiter(ReadOnlySpan<char> source, int start, ReadOnlySpan<char> delimiter)
    {
        for (var i = start; i <= source.Length - delimiter.Length; i++)
        {
            if (source[i] == '\\') { i++; continue; }
            if (At(source, i, delimiter) && CanClose(source, i, delimiter)) return i;
        }
        return -1;
    }

    static int FindBackticks(ReadOnlySpan<char> source, int start, int length)
    {
        for (var i = start; i <= source.Length - length; i++)
        {
            if (source[i] != '`') continue;
            var run = 1;
            while (i + run < source.Length && source[i + run] == '`') run++;
            if (run == length) return i;
            i += run - 1;
        }
        return -1;
    }

    static int FindBalanced(ReadOnlySpan<char> source, int start, char open, char close)
    {
        var depth = 0;
        for (var i = start; i < source.Length; i++)
        {
            if (source[i] == '\\') { i++; continue; }
            if (source[i] == open) depth++;
            else if (source[i] == close)
            {
                if (depth == 0) return i;
                depth--;
            }
        }
        return -1;
    }

    static ReadOnlySpan<char> Destination(ReadOnlySpan<char> value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '<')
        {
            var close = trimmed.IndexOf('>');
            return close > 1 ? trimmed[1..close] : default;
        }
        for (var i = 0; i < trimmed.Length; i++)
            if (char.IsWhiteSpace(trimmed[i])) return trimmed[..i];
        return trimmed;
    }

    static bool CanOpen(ReadOnlySpan<char> source, int start, ReadOnlySpan<char> delimiter)
    {
        var after = start + delimiter.Length;
        if (after >= source.Length || char.IsWhiteSpace(source[after])) return false;
        if (delimiter.Length == 1 && ((start > 0 && source[start - 1] == delimiter[0]) || source[after] == delimiter[0])) return false;
        return delimiter[0] != '_' || start == 0 || !char.IsLetterOrDigit(source[start - 1]) || !char.IsLetterOrDigit(source[after]);
    }

    static bool CanClose(ReadOnlySpan<char> source, int start, ReadOnlySpan<char> delimiter)
    {
        if (start == 0 || char.IsWhiteSpace(source[start - 1])) return false;
        var after = start + delimiter.Length;
        if (delimiter.Length == 1 && ((start > 0 && source[start - 1] == delimiter[0]) || (after < source.Length && source[after] == delimiter[0]))) return false;
        return delimiter[0] != '_' || after >= source.Length || !char.IsLetterOrDigit(source[start - 1]) || !char.IsLetterOrDigit(source[after]);
    }

    static bool TryFenceOpen(ReadOnlySpan<char> line, out char character, out int length)
    {
        character = default;
        length = 0;
        var trimmed = line.TrimStart();
        if (trimmed.Length < 3 || trimmed[0] is not ('`' or '~')) return false;
        character = trimmed[0];
        while (length < trimmed.Length && trimmed[length] == character) length++;
        return length >= 3;
    }

    static bool IsFenceClose(ReadOnlySpan<char> line, char character, int minimum)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < minimum) return false;
        foreach (var c in trimmed) if (c != character) return false;
        return true;
    }

    static bool TryOrderedMarker(ReadOnlySpan<char> source, out int length)
    {
        length = 0;
        while (length < source.Length && char.IsDigit(source[length])) length++;
        if (length == 0 || length + 1 >= source.Length || source[length] is not ('.' or ')') || source[length + 1] != ' ') return false;
        length++;
        return true;
    }

    static int LeadingSpaces(ReadOnlySpan<char> source, int maximum)
    {
        var count = 0;
        while (count < source.Length && count < maximum && source[count] == ' ') count++;
        return count;
    }

    static void Indent(StringBuilder output, int spaces)
    {
        for (var i = 0; i < spaces; i++) output.Append("&nbsp;");
    }

    static void OpenStyle(StringBuilder output, string style)
        => output.Append("<span style=\"").Append(style).Append("\">");

    static bool At(ReadOnlySpan<char> source, int start, ReadOnlySpan<char> value)
        => start >= 0 && start + value.Length <= source.Length && source.Slice(start, value.Length).SequenceEqual(value);

    static bool HasWhitespace(ReadOnlySpan<char> source)
    {
        foreach (var c in source) if (char.IsWhiteSpace(c)) return true;
        return false;
    }

    static bool IsBullet(char c) => c is '-' or '+' or '*';
    static bool IsInlineSpecial(char c) => c is '\\' or '&' or '<' or '>' or '`' or '[' or '*' or '_' or '~';
    static bool IsEscapable(char c) => c is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '<' or '>' or '(' or ')' or '#' or '+' or '-' or '.' or '!' or '|' or '~';

    static void Encode(StringBuilder output, ReadOnlySpan<char> source)
    {
        foreach (var c in source) Encode(output, c);
    }

    static void Encode(StringBuilder output, char c)
    {
        if (c == '&') output.Append("&amp;");
        else if (c == '<') output.Append("&lt;");
        else if (c == '>') output.Append("&gt;");
        else output.Append(c);
    }

    static void EncodeAttribute(StringBuilder output, ReadOnlySpan<char> source)
    {
        foreach (var c in source)
        {
            if (c == '&') output.Append("&amp;");
            else if (c == '<') output.Append("&lt;");
            else if (c == '>') output.Append("&gt;");
            else if (c == '"') output.Append("&quot;");
            else if (c == '\'') output.Append("&#39;");
            else output.Append(c);
        }
    }
}
