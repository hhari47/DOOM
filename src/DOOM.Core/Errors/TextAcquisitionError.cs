namespace DOOM.Core.Errors;

/// <summary>
/// Discrete failure codes for text acquisition operations.
/// </summary>
public enum TextAcquisitionError
{
    AccessibilityNotGranted,
    NoSourceApplication,
    OwnApplication,
    PasswordFieldDetected,
    EmptySelection,
    SelectionRetrievalFailed,
    Unsupported,
    EditorSelectionUnavailable,
    ClipboardFallbackDisabled,
    ClipboardAccessDenied,
    ClipboardNotPreservable,
    ClipboardCopyFailed,
    TooLong,
    TimedOut,
    NotText,
    Cancelled
}

/// <summary>
/// Exception thrown when text acquisition fails.
/// </summary>
public sealed class TextAcquisitionException : Exception
{
    public TextAcquisitionError ErrorCode { get; }
    public int? CharacterCount { get; }
    public int? CharacterLimit { get; }

    public TextAcquisitionException(TextAcquisitionError errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public TextAcquisitionException(TextAcquisitionError errorCode, int characterCount, int characterLimit)
        : base($"Selected text length ({characterCount}) exceeds maximum allowed limit of {characterLimit} characters.")
    {
        ErrorCode = errorCode;
        CharacterCount = characterCount;
        CharacterLimit = characterLimit;
    }
}

