namespace DOOM.Core.Contracts;

using DOOM.Core.State;

/// <summary>
/// Service contract for managing and verifying window capture exclusion via SetWindowDisplayAffinity.
/// </summary>
public interface IPrivacyProtectionService
{
    /// <summary>
    /// Current snapshot of privacy protection state.
    /// </summary>
    PrivacyProtectionState CurrentState { get; }

    /// <summary>
    /// Applies or removes the native display affinity on an HWND.
    /// </summary>
    /// <param name="hWnd">The target window handle.</param>
    /// <param name="enable">Whether to exclude the window from screen capture.</param>
    /// <returns>True if the OS accepted the affinity call; otherwise false.</returns>
    bool ApplyPrivacy(IntPtr hWnd, bool enable);

    /// <summary>
    /// Verifies what affinity is currently applied to the HWND by querying GetWindowDisplayAffinity.
    /// </summary>
    PrivacyVerificationStatus VerifyAppliedAffinity(IntPtr hWnd, bool? expectedRequested = null);

    /// <summary>
    /// Updates the current sharing context (e.g. SingleWindow vs EntireScreen).
    /// </summary>
    void UpdateSharingContext(SharingContext context);

    /// <summary>
    /// Event fired whenever the privacy protection state changes.
    /// </summary>
    event EventHandler<PrivacyProtectionState>? StateChanged;
}
