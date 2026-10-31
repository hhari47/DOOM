namespace DOOM.Presentation.Markdown;

public enum MarkdownTokenType
{
    Text,
    Bold,
    Italic,
    BoldItalic,
    Code,
    StrippedLink
}

/// <summary>
/// Represents an inline markdown token.
/// </summary>
public sealed record MarkdownInlineToken(MarkdownTokenType Type, string Text, string? Url = null);

