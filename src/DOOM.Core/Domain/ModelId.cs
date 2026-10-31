namespace DOOM.Core.Domain;

using System.Text.RegularExpressions;

/// <summary>
/// Validated Gemini Model Identifier.
/// </summary>
public sealed record ModelId
{
    private static readonly Regex ModelPattern = new(@"^[a-z0-9][a-z0-9.\-]{0,63}$", RegexOptions.Compiled);

    public static readonly ModelId Gemini35Flash = new("gemini-3.5-flash");
    public static readonly ModelId Gemini25Flash = new("gemini-2.5-flash");
    public static readonly ModelId Default = Gemini35Flash;

    public string Value { get; }

    public ModelId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !ModelPattern.IsMatch(value))
        {
            throw new ArgumentException($"Invalid Model ID format: '{value}'. Must match '^[a-z0-9][a-z0-9.\\-]{{0,63}}$'.", nameof(value));
        }

        Value = value;
    }

    public static bool TryCreate(string? candidate, out ModelId? modelId)
    {
        modelId = null;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim();
        if (!ModelPattern.IsMatch(trimmed))
        {
            return false;
        }

        modelId = new ModelId(trimmed);
        return true;
    }

    public override string ToString() => Value;
}

