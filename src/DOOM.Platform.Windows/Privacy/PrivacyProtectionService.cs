namespace DOOM.Platform.Windows.Privacy;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DOOM.Core.Contracts;
using DOOM.Core.State;
using DOOM.Platform.Windows.Native;
using Microsoft.Extensions.Logging;

/// <summary>
/// Controls native screen capture exclusion via SetWindowDisplayAffinity.
/// Separates requested state, OS-applied affinity, and self-test verification.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PrivacyProtectionService : IPrivacyProtectionService
{
    private readonly ILogger<PrivacyProtectionService> _logger;
    private readonly object _lock = new();
    private PrivacyProtectionState _state = PrivacyProtectionState.Default;

    public PrivacyProtectionState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public event EventHandler<PrivacyProtectionState>? StateChanged;

    public PrivacyProtectionService(ILogger<PrivacyProtectionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool ApplyPrivacy(IntPtr hWnd, bool enable)
    {
        if (hWnd == IntPtr.Zero)
        {
            _logger.LogWarning("ApplyPrivacy called with IntPtr.Zero HWND.");
            UpdateState(s => s with
            {
                IsRequested = enable,
                IsApplied = false,
                StatusReason = "Window handle is invalid."
            });
            return false;
        }

        var targetAffinity = enable ? Win32Interop.WDA_EXCLUDEFROMCAPTURE : Win32Interop.WDA_NONE;

        var success = Win32Interop.SetWindowDisplayAffinity(hWnd, targetAffinity);
        var lastError = success ? 0 : Marshal.GetLastWin32Error();

        if (!success)
        {
            _logger.LogError("SetWindowDisplayAffinity failed on HWND {Hwnd} with Win32 error {ErrorCode}.", hWnd, lastError);
            UpdateState(s => s with
            {
                IsRequested = enable,
                IsApplied = false,
                StatusReason = $"SetWindowDisplayAffinity error code: {lastError}"
            });
            return false;
        }

        // Verify with GetWindowDisplayAffinity against the target request state
        var verification = VerifyAppliedAffinity(hWnd, expectedRequested: enable);
        var opSucceeded = verification == PrivacyVerificationStatus.SelfTestPassed;
        var isApplied = enable && opSucceeded;

        _logger.LogInformation("Privacy mode {Status}: HWND={Hwnd}, Applied={Applied}, Affinity={Affinity}",
            enable ? "ENABLED" : "DISABLED", hWnd, isApplied, targetAffinity);

        UpdateState(s => s with
        {
            IsRequested = enable,
            IsApplied = isApplied,
            Verification = verification,
            StatusReason = opSucceeded ? null : "Affinity verification check failed.",
            LastVerificationTime = DateTimeOffset.UtcNow
        });

        return opSucceeded;
    }

    public PrivacyVerificationStatus VerifyAppliedAffinity(IntPtr hWnd, bool? expectedRequested = null)
    {
        if (hWnd == IntPtr.Zero)
        {
            return PrivacyVerificationStatus.Unavailable;
        }

        if (!Win32Interop.GetWindowDisplayAffinity(hWnd, out var appliedAffinity))
        {
            var err = Marshal.GetLastWin32Error();
            _logger.LogWarning("GetWindowDisplayAffinity failed with error {Error}.", err);
            return PrivacyVerificationStatus.Unavailable;
        }

        lock (_lock)
        {
            var checkRequested = expectedRequested ?? _state.IsRequested;
            if (checkRequested)
            {
                return appliedAffinity == Win32Interop.WDA_EXCLUDEFROMCAPTURE
                    ? PrivacyVerificationStatus.SelfTestPassed
                    : PrivacyVerificationStatus.SelfTestFailed;
            }

            return appliedAffinity == Win32Interop.WDA_NONE
                ? PrivacyVerificationStatus.SelfTestPassed
                : PrivacyVerificationStatus.SelfTestFailed;
        }
    }

    public void UpdateSharingContext(SharingContext context)
    {
        UpdateState(s => s with { Context = context });
        _logger.LogInformation("Sharing context updated to {Context}.", context);
    }

    private void UpdateState(Func<PrivacyProtectionState, PrivacyProtectionState> update)
    {
        PrivacyProtectionState newState;
        lock (_lock)
        {
            newState = update(_state);
            _state = newState;
        }

        StateChanged?.Invoke(this, newState);
    }
}
