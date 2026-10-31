namespace DOOM.UnitTests;

using DOOM.Presentation.Markdown;

public sealed class IncrementalMarkdownParserTests
{
    [Fact]
    public void ParseInlines_HyperlinksAreStrippedAndMarkedAsStrippedLink()
    {
        // Arrange (SEC-11 / UI-36)
        var markdown = "Visit [Google Search](https://google.com) or [Microsoft](https://microsoft.com) now.";

        // Act
        var tokens = IncrementalMarkdownParser.ParseInlines(markdown);

        // Assert
        Assert.Contains(tokens, t => t.Type == MarkdownTokenType.StrippedLink && t.Text == "Google Search");
        Assert.Contains(tokens, t => t.Type == MarkdownTokenType.StrippedLink && t.Text == "Microsoft");

        // Verify NO token contains the raw URL
        Assert.DoesNotContain(tokens, t => t.Text.Contains("https://google.com"));
        Assert.DoesNotContain(tokens, t => t.Text.Contains("https://microsoft.com"));
    }

    [Fact]
    public void ParseInlines_InlineFormattingParsedCorrectly()
    {
        // Arrange
        var markdown = "This has **bold text**, *italic text*, and `inline code`.";

        // Act
        var tokens = IncrementalMarkdownParser.ParseInlines(markdown);

        // Assert
        var bold = tokens.FirstOrDefault(t => t.Type == MarkdownTokenType.Bold);
        var italic = tokens.FirstOrDefault(t => t.Type == MarkdownTokenType.Italic);
        var code = tokens.FirstOrDefault(t => t.Type == MarkdownTokenType.Code);

        Assert.NotNull(bold);
        Assert.Equal("bold text", bold.Text);

        Assert.NotNull(italic);
        Assert.Equal("italic text", italic.Text);

        Assert.NotNull(code);
        Assert.Equal("inline code", code.Text);
    }

    [Fact]
    public void ParseInlines_BoldItalicParsedCorrectly()
    {
        // Arrange
        var markdown = "This is ***critical notice*** here.";

        // Act
        var tokens = IncrementalMarkdownParser.ParseInlines(markdown);

        // Assert
        var bi = tokens.FirstOrDefault(t => t.Type == MarkdownTokenType.BoldItalic);
        Assert.NotNull(bi);
        Assert.Equal("critical notice", bi.Text);
    }

    [Fact]
    public void ParseInlines_PlainTextPassesThroughCleanly()
    {
        // Arrange
        var plain = "This is simple plain text without any markdown symbols.";

        // Act
        var tokens = IncrementalMarkdownParser.ParseInlines(plain);

        // Assert
        Assert.Single(tokens);
        Assert.Equal(MarkdownTokenType.Text, tokens[0].Type);
        Assert.Equal(plain, tokens[0].Text);
    }

    [Fact]
    public void ParseBlocks_BulletListsAndNumberedListsRecognized()
    {
        // Arrange
        var markdown = "- Item one\n* Item two\n• Item three\n1. Numbered item";

        // Act
        var blocks = IncrementalMarkdownParser.ParseBlocks(markdown);

        // Assert
        Assert.Equal(4, blocks.Count);
        Assert.Equal(MarkdownBlockType.BulletItem, blocks[0].Type);
        Assert.Equal("• ", blocks[0].Prefix);
        Assert.Equal("Item one", blocks[0].Inlines[0].Text);

        Assert.Equal(MarkdownBlockType.BulletItem, blocks[1].Type);
        Assert.Equal("Item two", blocks[1].Inlines[0].Text);

        Assert.Equal(MarkdownBlockType.BulletItem, blocks[2].Type);
        Assert.Equal("Item three", blocks[2].Inlines[0].Text);

        Assert.Equal(MarkdownBlockType.NumberedItem, blocks[3].Type);
        Assert.Equal("1. ", blocks[3].Prefix);
        Assert.Equal("Numbered item", blocks[3].Inlines[0].Text);
    }

    [Fact]
    public void ParseBlocks_PreservesBlankLinesBetweenParagraphs()
    {
        // Arrange
        var multiline = "Paragraph 1\n\nParagraph 2";

        // Act
        var blocks = IncrementalMarkdownParser.ParseBlocks(multiline);

        // Assert
        Assert.Equal(3, blocks.Count);
        Assert.Equal(MarkdownBlockType.Paragraph, blocks[0].Type);
        Assert.Equal(MarkdownBlockType.BlankLine, blocks[1].Type);
        Assert.Equal(MarkdownBlockType.Paragraph, blocks[2].Type);
    }

    [Fact]
    public void ParseBlocks_EmptyOrNullReturnsEmpty()
    {
        Assert.Empty(IncrementalMarkdownParser.ParseBlocks(string.Empty));
        Assert.Empty(IncrementalMarkdownParser.ParseInlines(string.Empty));
    }
}

