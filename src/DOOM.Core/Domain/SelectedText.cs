namespace DOOM.Core.Domain;

/// <summary>
/// Method used to capture the selected text.
/// </summary>
public enum AcquisitionMethod
{
    UiAutomation,
    ClipboardFallback
}

/// <summary>
/// Represents validated text acquired from an external application.
/// </summary>
public sealed record SelectedText
{
    public string Content { get; }
    public int CharacterCount { get; }
    public SourceApplication Source { get; }
    public AcquisitionMethod Method { get; }
    public bool RestoreSkipped { get; }
    public DateTimeOffset AcquiredAt { get; }

    public SelectedText(
        string content,
        SourceApplication source,
        AcquisitionMethod method,
        bool restoreSkipped = false,
        DateTimeOffset? acquiredAt = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        CharacterCount = content.Length;
        Source = source ?? SourceApplication.Unknown;
        Method = method;
        RestoreSkipped = restoreSkipped;
        AcquiredAt = acquiredAt ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Redacted representation to prevent full text leaks in default string conversions.
    /// </summary>
    public override string ToString() => $"SelectedText({CharacterCount} chars, Method={Method}, Source={Source})";
}

