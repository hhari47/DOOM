namespace DOOM.UnitTests;

using DOOM.Core.Domain;
using Xunit;

public class ApiKeyTests
{
    [Theory]
    [InlineData("AIzaSyD-1234567890abcdefghijklmnopqrstuv")]
    [InlineData("abcdefghijklmnopqrstuvwx")]
    [InlineData("12345678901234567890")] // Exactly 20 chars (minimum)
    [InlineData("AIzaSyD_test-1234.5678+90=foo/bar!@#$")] // Special symbols permitted without undocumented restrictions
    public void TryCreate_ValidKey_ReturnsTrue(string validKey)
    {
        var success = ApiKey.TryCreate(validKey, out var apiKey, out var errorMessage);

        Assert.True(success);
        Assert.NotNull(apiKey);
        Assert.Null(errorMessage);
        Assert.Equal(validKey, apiKey!.AccessKey());
    }

    [Fact]
    public void TryCreate_Exactly256Chars_ReturnsTrue()
    {
        var maxKey = new string('A', 256);
        var success = ApiKey.TryCreate(maxKey, out var apiKey, out var errorMessage);

        Assert.True(success);
        Assert.NotNull(apiKey);
        Assert.Null(errorMessage);
        Assert.Equal(256, apiKey!.AccessKey().Length);
    }

    [Theory]
    [InlineData("  AIzaSyD-1234567890abcdefghijklmnopqrstuv  ", "AIzaSyD-1234567890abcdefghijklmnopqrstuv")]
    [InlineData("\r\n\tAIzaSyD-1234567890abcdefghijklmnopqrstuv\n\t ", "AIzaSyD-1234567890abcdefghijklmnopqrstuv")]
    public void TryCreate_SurroundingWhitespace_TrimsAndPreservesKey(string rawInput, string expectedKey)
    {
        var success = ApiKey.TryCreate(rawInput, out var apiKey, out var errorMessage);

        Assert.True(success);
        Assert.NotNull(apiKey);
        Assert.Null(errorMessage);
        Assert.Equal(expectedKey, apiKey!.AccessKey());
    }

    [Theory]
    [InlineData(null, "API key cannot be empty.")]
    [InlineData("", "API key cannot be empty.")]
    [InlineData("   ", "API key cannot be empty.")]
    [InlineData("short", "API key is too short (5 characters). Must be at least 20 characters.")]
    public void TryCreate_InvalidKey_ReturnsFalseWithDetailedMessage(string? invalidKey, string expectedErrorSubstring)
    {
        var success = ApiKey.TryCreate(invalidKey, out var apiKey, out var errorMessage);

        Assert.False(success);
        Assert.Null(apiKey);
        Assert.NotNull(errorMessage);
        Assert.Contains(expectedErrorSubstring, errorMessage);
    }

    [Fact]
    public void TryCreate_19Chars_ReturnsFalseTooShort()
    {
        var key19 = new string('x', 19);
        var success = ApiKey.TryCreate(key19, out var apiKey, out var errorMessage);

        Assert.False(success);
        Assert.Null(apiKey);
        Assert.NotNull(errorMessage);
        Assert.Contains("too short (19 characters)", errorMessage);
    }

    [Fact]
    public void TryCreate_257Chars_ReturnsFalseTooLong()
    {
        var key257 = new string('x', 257);
        var success = ApiKey.TryCreate(key257, out var apiKey, out var errorMessage);

        Assert.False(success);
        Assert.Null(apiKey);
        Assert.NotNull(errorMessage);
        Assert.Contains("too long (257 characters)", errorMessage);
    }

    [Fact]
    public void ToString_AlwaysReturnsRedacted()
    {
        var key = ApiKey.Create("AIzaSyD-1234567890abcdefghijklmnopqrstuv");

        Assert.Equal("<redacted>", key.ToString());
        Assert.DoesNotContain("AIza", key.ToString());
    }

    [Fact]
    public void Create_InvalidKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ApiKey.Create("too_short"));
    }
}

