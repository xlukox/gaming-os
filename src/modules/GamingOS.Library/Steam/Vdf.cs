using System.Text;

namespace GamingOS.Library.Steam;

/// <summary>
/// Reader for Valve's text KeyValues format (.vdf, .acf): quoted keys followed by a quoted value or a { } block.
/// A block becomes a case-insensitive dictionary; a value becomes a string.
/// </summary>
public static class Vdf
{
    // Compared by reference, so a quoted "{" in the file is never mistaken for a brace.
    private static readonly string Open = new('{', 1);
    private static readonly string Close = new('}', 1);

    /// <exception cref="FormatException">The text is not valid KeyValues.</exception>
    public static Dictionary<string, object> Parse(string text)
    {
        var position = 0;
        return ParseBlock(text, ref position, isRoot: true);
    }

    private static Dictionary<string, object> ParseBlock(string text, ref int position, bool isRoot)
    {
        var block = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var key = NextToken(text, ref position);
            if (key is null)
                return isRoot ? block : throw new FormatException("A '{' is never closed.");
            if (ReferenceEquals(key, Close))
                return isRoot ? throw new FormatException("Unexpected '}'.") : block;
            if (ReferenceEquals(key, Open))
                throw new FormatException("A '{' appears where a key is expected.");

            var value = NextToken(text, ref position);
            if (value is null || ReferenceEquals(value, Close))
                throw new FormatException($"Key '{key}' has no value.");
            block[key] = ReferenceEquals(value, Open) ? ParseBlock(text, ref position, isRoot: false) : value;
        }
    }

    private static string? NextToken(string text, ref int position)
    {
        while (position < text.Length)
        {
            if (char.IsWhiteSpace(text[position]))
                position++;
            else if (text[position] == '/' && position + 1 < text.Length && text[position + 1] == '/')
                while (position < text.Length && text[position] != '\n') position++;
            else
                break;
        }
        if (position >= text.Length) return null;

        switch (text[position])
        {
            case '{':
                position++;
                return Open;
            case '}':
                position++;
                return Close;
            case '"':
                return QuotedToken(text, ref position);
        }

        var start = position;
        while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] is not ('{' or '}' or '"'))
            position++;
        return text[start..position];
    }

    private static string QuotedToken(string text, ref int position)
    {
        var token = new StringBuilder();
        for (position++; position < text.Length; position++)
        {
            var c = text[position];
            if (c == '"')
            {
                position++;
                return token.ToString();
            }
            if (c == '\\' && position + 1 < text.Length)
            {
                // Windows paths are written as C:\\Games, so the escape must be undone.
                c = text[++position] switch { 'n' => '\n', 't' => '\t', var other => other };
            }
            token.Append(c);
        }
        throw new FormatException("A quoted value is never closed.");
    }
}
