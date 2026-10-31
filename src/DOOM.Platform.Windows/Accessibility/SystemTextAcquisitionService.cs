namespace DOOM.Platform.Windows.Accessibility;

using System.Diagnostics;
using System.Runtime.Versioning;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.Errors;
using DOOM.Platform.Windows.Clipboard;
using DOOM.Platform.Windows.Native;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates accessibility-first text acquisition with opt-in clipboard fallback.
/// Strictly enforces security boundaries (e.g. password fields never fall back to clipboard).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SystemTextAcquisitionService : ITextAcquisitionService, IDisposable
{
    private readonly UiAutomationSelectionReader _uiaReader;
    private readonly ClipboardFallbackHandler _clipboardHandler;
    private readonly ILogger<SystemTextAcquisitionService> _logger;

    public SystemTextAcquisitionService(
        UiAutomationSelectionReader uiaReader,
        ClipboardFallbackHandler clipboardHandler,
        ILogger<SystemTextAcquisitionService> logger)
    {
        _uiaReader = uiaReader ?? throw new ArgumentNullException(nameof(uiaReader));
        _clipboardHandler = clipboardHandler ?? throw new ArgumentNullException(nameof(clipboardHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SelectedText> AcquireSelectionAsync(bool allowClipboardFallback, CancellationToken cancellationToken = default)
    {
        // 1. Primary Strategy: UI Automation
        try
        {
            _logger.LogDebug("Attempting primary text acquisition via UI Automation.");
            var result = await _uiaReader.ReadSelectionAsync(cancellationToken);
            _logger.LogInformation("Successfully acquired {Count} characters via UI Automation from {Source}.", result.CharacterCount, result.Source);
            return result;
        }
        catch (TextAcquisitionException ex) when (ex.ErrorCode == TextAcquisitionError.PasswordFieldDetected)
        {
            // CRITICAL SECURITY RULE: Password fields must NEVER fall back to clipboard copy
            _logger.LogWarning("Text acquisition aborted: Password field detected. Fallback barred.");
            throw;
        }
        catch (TextAcquisitionException ex) when (ex.ErrorCode == TextAcquisitionError.TooLong)
        {
            // Selection limits exceeded: do not fall back
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Primary UI Automation acquisition unavailable ({Message}). Evaluating fallback.", ex.Message);

            if (!allowClipboardFallback)
            {
                _logger.LogInformation("Clipboard fallback is disabled by configuration. Aborting acquisition.");
                throw new TextAcquisitionException(
                    TextAcquisitionError.ClipboardFallbackDisabled,
                    "Application does not expose selection via UI Automation and Clipboard Fallback is disabled in Settings.",
                    ex);
            }

            // 2. Secondary Strategy: Opt-In Clipboard Fallback
            _logger.LogInformation("Executing opt-in clipboard fallback acquisition...");
            var foregroundHwnd = Win32Interop.GetForegroundWindow();
            var sourceApp = ResolveSourceApp(foregroundHwnd);

            var fallbackResult = await _clipboardHandler.AcquireViaClipboardAsync(sourceApp, cancellationToken);
            _logger.LogInformation("Successfully acquired {Count} characters via Clipboard Fallback (RestoreSkipped={Skipped}).",
                fallbackResult.CharacterCount, fallbackResult.RestoreSkipped);

            return fallbackResult;
        }
    }

    private static SourceApplication ResolveSourceApp(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return SourceApplication.Unknown;
        }

        Win32Interop.GetWindowThreadProcessId(hWnd, out var pid);
        if (pid == 0)
        {
            return SourceApplication.Unknown;
        }

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return new SourceApplication((int)pid, proc.ProcessName, proc.MainWindowTitle, proc.MainModule?.FileName);
        }
        catch
        {
            return new SourceApplication((int)pid, $"PID_{pid}", null, null);
        }
    }

    public void Dispose()
    {
        _uiaReader.Dispose();
    }
}

