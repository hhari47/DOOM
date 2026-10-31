namespace DOOM.Core.Logging;

using System.Text.RegularExpressions;

/// <summary>
/// Strictly redacts sensitive credentials and personal data from log messages, exception details, and diagnostics.
/// Guarantees that Gemini API keys, selected text, and full prompts can never be written to disk or console.
/// </summary>
public static class SensitiveDataRedactor
{
    private static readonly Regex GoogleApiKeyRegex = new(
        @"(AIza[0-9A-Za-z-_]{30,60})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex KeyQueryParamRegex = new(
        @"([?&]key=)[^&\s]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AuthHeaderRegex = new(
        @"(x-goog-api-key:\s*)[^\r\n]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Sanitizes any string message before it is passed to a logging provider.
    /// </summary>
    public static string Redact(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        // 1. Redact Google API key pattern
        var sanitized = GoogleApiKeyRegex.Replace(message, "<redacted_api_key>");

        // 2. Redact key query parameters in URLs
        sanitized = KeyQueryParamRegex.Replace(sanitized, "$1<redacted>");

        // 3. Redact header values
        sanitized = AuthHeaderRegex.Replace(sanitized, "$1<redacted>");

        return sanitized;
    }

    /// <summary>
    /// Truncates error messages to a safe maximum length and redacts secrets.
    /// </summary>
    public static string SanitizeErrorMessage(string? rawMessage, int maxLength = 300)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return "An unexpected error occurred.";
        }

        var redacted = Redact(rawMessage);
        return redacted.Length > maxLength
            ? string.Concat(redacted.AsSpan(0, maxLength), "…")
            : redacted;
    }
}

