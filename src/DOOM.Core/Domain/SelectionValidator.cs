namespace DOOM.Core.Domain;

using DOOM.Core.Errors;

/// <summary>
/// Validates and normalizes acquired text selections before submission to Gemini.
/// </summary>
public static class SelectionValidator
{
    /// <summary>
    /// Validates raw text captured from an external window.
    /// </summary>
    public static SelectedText Validate(
        string? rawText,
        SourceApplication source,
        AcquisitionMethod method,
        bool restoreSkipped = false)
    {
        if (rawText is null)
        {
            throw new TextAcquisitionException(TextAcquisitionError.EmptySelection, "No text selection was captured.");
        }

        // 1. Strip NUL characters
        var sanitized = rawText.Replace("\0", string.Empty);

        // 2. Trim whitespace
        sanitized = sanitized.Trim();

        // 3. Check for empty
        if (sanitized.Length == 0)
        {
            throw new TextAcquisitionException(TextAcquisitionError.EmptySelection, "Selected text is empty after trimming whitespace.");
        }

        // 4. Check upper bounds
        if (sanitized.Length > SelectionLimits.MaxCharacters)
        {
            throw new TextAcquisitionException(
                TextAcquisitionError.TooLong,
                sanitized.Length,
                SelectionLimits.MaxCharacters);
        }

        return new SelectedText(sanitized, source, method, restoreSkipped);
    }
}

