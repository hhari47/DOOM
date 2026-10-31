namespace DOOM.Presentation.Markdown;

using System.Text.RegularExpressions;

public enum MarkdownBlockType
{
    Paragraph,
    BulletItem,
    NumberedItem,
    BlankLine
}

/// <summary>
/// Structured block of parsed markdown content.
/// </summary>
public sealed record MarkdownBlock(MarkdownBlockType Type, IReadOnlyList<MarkdownInlineToken> Inlines, string? Prefix = null);

/// <summary>
/// Incremental parser for DOOM markdown responses.
/// Enforces UI-36 and SEC-11 (stripped links, inline-only formatting, whitespace preservation).
/// Pure domain logic: zero WPF dependencies for headless unit testing.
/// </summary>
public static partial class IncrementalMarkdownParser
{
    // Combined inline token regex matching links, bold-italic, bold, italic, and inline code
    private static readonly Regex InlineRegex = new(
        @"(?<link>\[(?<linkText>[^\]]+)\]\((?<linkUrl>[^)]+)\))|" +
        @"(?<boldItalic>\*\*\*(?<biText>.+?)\*\*\*|___(?<biText2>.+?)___)|" +
        @"(?<bold>\*\*(?<bText>.+?)\*\*|__(?<bText2>.+?)__)|" +
        @"(?<italic>\*(?<iText>[^*\n]+?)\*|_(?<iText2>[^_\n]+?)_)|" +
        @"(?<code>`(?<cText>[^`\n]+?)`)",
        RegexOptions.Compiled);

    private static readonly Regex BulletRegex = new(@"^\s*[-*•]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex NumberedRegex = new(@"^\s*(\d+\.)\s+(.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Parses a complete markdown string into structured blocks.
    /// </summary>
    public static IReadOnlyList<MarkdownBlock> ParseBlocks(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return Array.Empty<MarkdownBlock>();
        }

        var lines = raw.Replace("\r\n", "\n").Split('\n');
        var blocks = new List<MarkdownBlock>(lines.Length);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                blocks.Add(new MarkdownBlock(MarkdownBlockType.BlankLine, Array.Empty<MarkdownInlineToken>()));
                continue;
            }

            var bulletMatch = BulletRegex.Match(line);
            if (bulletMatch.Success)
            {
                var content = bulletMatch.Groups[1].Value;
                var inlines = ParseInlines(content);
                blocks.Add(new MarkdownBlock(MarkdownBlockType.BulletItem, inlines, "• "));
                continue;
            }

            var numMatch = NumberedRegex.Match(line);
            if (numMatch.Success)
            {
                var prefix = numMatch.Groups[1].Value + " ";
                var content = numMatch.Groups[2].Value;
                var inlines = ParseInlines(content);
                blocks.Add(new MarkdownBlock(MarkdownBlockType.NumberedItem, inlines, prefix));
                continue;
            }

            // Normal paragraph line
            blocks.Add(new MarkdownBlock(MarkdownBlockType.Paragraph, ParseInlines(line)));
        }

        return blocks;
    }

    /// <summary>
    /// Parses a single line or inline text span into tokens.
    /// </summary>
    public static IReadOnlyList<MarkdownInlineToken> ParseInlines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<MarkdownInlineToken>();
        }

        var tokens = new List<MarkdownInlineToken>();
        var matches = InlineRegex.Matches(text);
        int lastIndex = 0;

        foreach (Match match in matches)
        {
            // Text preceding the match
            if (match.Index > lastIndex)
            {
                var preceding = text.Substring(lastIndex, match.Index - lastIndex);
                tokens.Add(new MarkdownInlineToken(MarkdownTokenType.Text, preceding));
            }

            if (match.Groups["link"].Success)
            {
                var linkText = match.Groups["linkText"].Value;
                // SEC-11: We capture link text but purposefully do not emit clickable links.
                tokens.Add(new MarkdownInlineToken(MarkdownTokenType.StrippedLink, linkText));
            }
            else if (match.Groups["boldItalic"].Success)
            {
                var bi = match.Groups["biText"].Success ? match.Groups["biText"].Value : match.Groups["biText2"].Value;
                tokens.Add(new MarkdownInlineToken(MarkdownTokenType.BoldItalic, bi));
            }
            else if (match.Groups["bold"].Success)
            {
                var b = match.Groups["bText"].Success ? match.Groups["bText"].Value : match.Groups["bText2"].Value;
                tokens.Add(new MarkdownInlineToken(MarkdownTokenType.Bold, b));
            }
            else if (match.Groups["italic"].Success)
            {
                var i = match.Groups["iText"].Success ? match.Groups["iText"].Value : match.Groups["iText2"].Value;
                tokens.Add(new MarkdownInlineToken(MarkdownTokenType.Italic, i));
            }
            else if (match.Groups["code"].Success)
            {
                var c = match.Groups["cText"].Value;
                tokens.Add(new MarkdownInlineToken(MarkdownTokenType.Code, c));
            }

            lastIndex = match.Index + match.Length;
        }

        // Remaining text after last match
        if (lastIndex < text.Length)
        {
            var trailing = text.Substring(lastIndex);
            tokens.Add(new MarkdownInlineToken(MarkdownTokenType.Text, trailing));
        }

        return tokens;
    }
}

