namespace DOOM.UnitTests;

using DOOM.Core.Domain;
using DOOM.Core.Errors;

public class SelectionValidatorTests
{
    private static readonly SourceApplication TestSource = new(100, "TestApp", "Test Title", "C:\\TestApp.exe");

    [Fact]
    public void Validate_ValidText_ReturnsSelectedText()
    {
        var raw = "  Hello, DOOM AI assistant!  ";
        var result = SelectionValidator.Validate(raw, TestSource, AcquisitionMethod.UiAutomation);

        Assert.Equal("Hello, DOOM AI assistant!", result.Content);
        Assert.Equal("Hello, DOOM AI assistant!".Length, result.CharacterCount);
        Assert.Equal(TestSource, result.Source);
        Assert.Equal(AcquisitionMethod.UiAutomation, result.Method);
        Assert.False(result.RestoreSkipped);
    }

    [Fact]
    public void Validate_TextWithNulCharacters_StripsNulCharacters()
    {
        var raw = "Text\0with\0Nul";
        var result = SelectionValidator.Validate(raw, TestSource, AcquisitionMethod.UiAutomation);

        Assert.Equal("TextwithNul", result.Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\0\0")]
    public void Validate_EmptyOrWhitespace_ThrowsEmptySelection(string? emptyText)
    {
        var ex = Assert.Throws<TextAcquisitionException>(() =>
            SelectionValidator.Validate(emptyText, TestSource, AcquisitionMethod.UiAutomation));

        Assert.Equal(TextAcquisitionError.EmptySelection, ex.ErrorCode);
    }

    [Fact]
    public void Validate_Exceeding20000Characters_ThrowsTooLong()
    {
        var tooLong = new string('A', SelectionLimits.MaxCharacters + 1);

        var ex = Assert.Throws<TextAcquisitionException>(() =>
            SelectionValidator.Validate(tooLong, TestSource, AcquisitionMethod.UiAutomation));

        Assert.Equal(TextAcquisitionError.TooLong, ex.ErrorCode);
        Assert.Equal(20_001, ex.CharacterCount);
        Assert.Equal(SelectionLimits.MaxCharacters, ex.CharacterLimit);
    }

    [Fact]
    public void Validate_WithRestoreSkippedTrue_PreservesMetadata()
    {
        var raw = "Fallback clipboard text";
        var result = SelectionValidator.Validate(raw, TestSource, AcquisitionMethod.ClipboardFallback, restoreSkipped: true);

        Assert.True(result.RestoreSkipped);
        Assert.Equal(AcquisitionMethod.ClipboardFallback, result.Method);
    }
}

