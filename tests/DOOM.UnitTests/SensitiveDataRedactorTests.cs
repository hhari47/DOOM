namespace DOOM.UnitTests;

using DOOM.Core.Logging;

public class SensitiveDataRedactorTests
{
    [Fact]
    public void Redact_GoogleApiKeyPattern_ReplacesWithPlaceholder()
    {
        var raw = "Sending request with AIzaSyD-1234567890abcdefghijklmnopq to Gemini backend.";
        var sanitized = SensitiveDataRedactor.Redact(raw);

        Assert.DoesNotContain("AIzaSyD-1234567890abcdefghijklmnopq", sanitized);
        Assert.Contains("<redacted_api_key>", sanitized);
    }

    [Fact]
    public void Redact_QueryParamKey_ReplacesValue()
    {
        var url = "https://generativelanguage.googleapis.com/v1beta/interactions?alt=sse&key=AIzaSySecretKey123456789";
        var sanitized = SensitiveDataRedactor.Redact(url);

        Assert.DoesNotContain("AIzaSySecretKey123456789", sanitized);
        Assert.Contains("key=<redacted>", sanitized);
    }

    [Fact]
    public void Redact_AuthorizationHeader_ReplacesValue()
    {
        var header = "x-goog-api-key: AIzaSyD-secret-token-value-here";
        var sanitized = SensitiveDataRedactor.Redact(header);

        Assert.DoesNotContain("AIzaSyD-secret-token-value-here", sanitized);
        Assert.Contains("x-goog-api-key: <redacted>", sanitized);
    }

    [Fact]
    public void SanitizeErrorMessage_LongMessage_TruncatesAndRedacts()
    {
        var longKey = "AIzaSyD-1234567890abcdefghijklmnopq";
        var message = "Error: " + longKey + " occurred while calling service with details: " + new string('X', 500);

        var sanitized = SensitiveDataRedactor.SanitizeErrorMessage(message, maxLength: 100);

        Assert.DoesNotContain(longKey, sanitized);
        Assert.True(sanitized.Length <= 102); // 100 + ellipsis char
        Assert.EndsWith("…", sanitized);
    }
}

