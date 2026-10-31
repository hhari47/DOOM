namespace DOOM.Core.Domain;

/// <summary>
/// Strongly-typed, redacted wrapper for Gemini API keys.
/// Guarantees that API keys are validated and cannot be exposed through standard string formatting or logging.
/// </summary>
public sealed record ApiKey
{
    private readonly string _rawValue;

    private ApiKey(string rawValue)
    {
        _rawValue = rawValue;
    }

    /// <summary>
    /// Attempts to validate and construct an ApiKey.
    /// </summary>
    /// <param name="rawKey">The raw key candidate.</param>
    /// <param name="apiKey">The validated ApiKey instance if successful.</param>
    /// <returns>True if valid; otherwise false.</returns>
    public static bool TryCreate(string? rawKey, out ApiKey? apiKey)
    {
        return TryCreate(rawKey, out apiKey, out _);
    }

    /// <summary>
    /// Attempts to validate and construct an ApiKey, returning a helpful validation error on failure.
    /// </summary>
    /// <param name="rawKey">The raw key candidate.</param>
    /// <param name="apiKey">The validated ApiKey instance if successful.</param>
    /// <param name="errorMessage">Detailed reason if validation failed.</param>
    /// <returns>True if valid; otherwise false.</returns>
    public static bool TryCreate(string? rawKey, out ApiKey? apiKey, out string? errorMessage)
    {
        apiKey = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(rawKey))
        {
            errorMessage = "API key cannot be empty.";
            return false;
        }

        var trimmed = rawKey.Trim();

        if (trimmed.Length < 20)
        {
            errorMessage = $"API key is too short ({trimmed.Length} characters). Must be at least 20 characters.";
            return false;
        }

        if (trimmed.Length > 256)
        {
            errorMessage = $"API key is too long ({trimmed.Length} characters). Must be at most 256 characters.";
            return false;
        }

        apiKey = new ApiKey(trimmed);
        return true;
    }

    /// <summary>
    /// Creates a validated ApiKey or throws ArgumentException.
    /// </summary>
    public static ApiKey Create(string rawKey)
    {
        if (!TryCreate(rawKey, out var apiKey, out var errorMessage))
        {
            throw new ArgumentException(errorMessage ?? "API key format is invalid. Must be 20-256 characters.", nameof(rawKey));
        }

        return apiKey!;
    }

    /// <summary>
    /// Provides access to the raw key string strictly for HTTP authorization header injection.
    /// Never call this for logging, telemetry, or presentation.
    /// </summary>
    public string AccessKey() => _rawValue;

    /// <summary>
    /// Always returns a redacted placeholder to prevent accidental credential leakage in logs or format strings.
    /// </summary>
    public override string ToString() => "<redacted>";
}

