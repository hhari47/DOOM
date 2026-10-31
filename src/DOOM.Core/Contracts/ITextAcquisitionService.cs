namespace DOOM.Core.Contracts;

using DOOM.Core.Domain;

/// <summary>
/// Service contract for acquiring selected text from the foreground application.
/// Strictly enforces accessibility-first, optional clipboard fallback, and password field exclusion.
/// </summary>
public interface ITextAcquisitionService
{
    /// <summary>
    /// Acquires selected text using UI Automation with optional clipboard fallback.
    /// Guarantees that any native unmanaged calls cannot hang or accumulate blocked worker threads.
    /// </summary>
    Task<SelectedText> AcquireSelectionAsync(bool allowClipboardFallback, CancellationToken cancellationToken = default);
}

