namespace DOOM.Platform.Windows.Accessibility;

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows.Automation;
using DOOM.Core.Domain;
using DOOM.Core.Errors;
using DOOM.Platform.Windows.Native;
using Microsoft.Extensions.Logging;

/// <summary>
/// Acquires selected text from the focused control using Windows UI Automation (UIA).
/// Guarantees dedicated STA worker isolation, password-field exclusion, and watchdog timeout prevention.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UiAutomationSelectionReader : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(1000);

    private readonly ILogger<UiAutomationSelectionReader> _logger;
    private readonly SemaphoreSlim _concurrencyGate = new(1, 1);
    private bool _isDisposed;

    public UiAutomationSelectionReader(ILogger<UiAutomationSelectionReader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Attempts to retrieve the current text selection via UI Automation.
    /// Runs on an isolated STA thread with a hard timeout to prevent accumulation of hung unmanaged calls.
    /// </summary>
    public async Task<SelectedText> ReadSelectionAsync(CancellationToken cancellationToken = default)
    {
        // 1. Acquire single-flight gate; reject or wait boundedly
        if (!await _concurrencyGate.WaitAsync(TimeSpan.FromMilliseconds(100), cancellationToken))
        {
            _logger.LogWarning("UI Automation worker busy. Dropping concurrent request.");
            throw new TextAcquisitionException(TextAcquisitionError.SelectionRetrievalFailed, "UI Automation worker is already active.");
        }

        try
        {
            var tcs = new TaskCompletionSource<SelectedText>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Create dedicated STA thread for COM UIA interaction
            var staThread = new Thread(() =>
            {
                try
                {
                    var result = ExecuteUiaRead();
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            })
            {
                Name = "DOOM_UIA_Worker",
                IsBackground = true
            };

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();

            // Watchdog: Timeout after 1000ms
            using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linkedCts.Token));

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            if (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning("UI Automation call timed out after {Timeout}ms on external application.", DefaultTimeout.TotalMilliseconds);
                throw new TextAcquisitionException(TextAcquisitionError.TimedOut, "UI Automation query timed out.");
            }

            throw new TextAcquisitionException(TextAcquisitionError.Cancelled, "UI Automation query was cancelled.");
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    private SelectedText ExecuteUiaRead()
    {
        // 1. Provenance: Identify foreground window & process
        var foregroundHwnd = Win32Interop.GetForegroundWindow();
        var sourceApp = ResolveSourceApplication(foregroundHwnd);

        // Reject if target application is DOOM itself
        var currentPid = Environment.ProcessId;
        if (sourceApp.ProcessId == currentPid)
        {
            throw new TextAcquisitionException(TextAcquisitionError.OwnApplication, "Selection belongs to DOOM.");
        }

        // 2. Query Focused Element
        AutomationElement? focusedElement;
        try
        {
            focusedElement = AutomationElement.FocusedElement;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve AutomationElement.FocusedElement.");
            throw new TextAcquisitionException(TextAcquisitionError.SelectionRetrievalFailed, "Failed to query focused UI element.", ex);
        }

        if (focusedElement == null)
        {
            throw new TextAcquisitionException(TextAcquisitionError.EmptySelection, "No focused UI element found.");
        }

        // 3. SEC-01: Password Field Exclusion (Dual-Layer: UIA IsPassword + Win32 ES_PASSWORD)
        try
        {
            bool isPassword = focusedElement.Current.IsPassword;

            if (!isPassword && focusedElement.Current.NativeWindowHandle != 0)
            {
                var hNative = new IntPtr(focusedElement.Current.NativeWindowHandle);
                var style = (long)Win32Interop.GetWindowLongPtr(hNative, Win32Interop.GWL_STYLE);
                if ((style & Win32Interop.ES_PASSWORD) != 0)
                {
                    isPassword = true;
                }
            }

            if (isPassword)
            {
                _logger.LogWarning("Text acquisition blocked: focused element is flagged as password field.");
                throw new TextAcquisitionException(TextAcquisitionError.PasswordFieldDetected, "Text capture is blocked in password fields for your security.");
            }
        }
        catch (Exception ex) when (ex is not TextAcquisitionException)
        {
            _logger.LogDebug(ex, "Could not query IsPassword property directly.");
        }

        // 4. Strategy A: TextPattern
        string? rawSelection = null;
        if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out var patternObj) && patternObj is TextPattern textPattern)
        {
            try
            {
                var selectionRanges = textPattern.GetSelection();
                if (selectionRanges != null && selectionRanges.Length > 0)
                {
                    rawSelection = selectionRanges[0].GetText(-1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TextPattern.GetSelection failed on focused element.");
            }
        }

        // 5. Strategy B: ValuePattern fallback (for simple single-line edit boxes that don't implement TextPattern)
        if (string.IsNullOrWhiteSpace(rawSelection) &&
            focusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out var valPatternObj) &&
            valPatternObj is ValuePattern valPattern)
        {
            try
            {
                // Only use value if it's an edit control without TextPattern
                if (focusedElement.Current.ControlType == ControlType.Edit)
                {
                    rawSelection = valPattern.Current.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ValuePattern retrieval failed.");
            }
        }

        if (rawSelection == null)
        {
            throw new TextAcquisitionException(TextAcquisitionError.Unsupported, "The focused control does not expose a compatible TextPattern.");
        }

        // 6. Validate and enforce limits
        return SelectionValidator.Validate(rawSelection, sourceApp, AcquisitionMethod.UiAutomation);
    }

    private SourceApplication ResolveSourceApplication(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return SourceApplication.Unknown;
        }

        Win32Interop.GetWindowThreadProcessId(hWnd, out var processId);
        if (processId == 0)
        {
            return SourceApplication.Unknown;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName;
            var windowTitle = process.MainWindowTitle;
            string? execPath = null;

            try { execPath = process.MainModule?.FileName; } catch { /* Ignore access denied for system processes */ }

            return new SourceApplication((int)processId, processName, windowTitle, execPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve full process info for PID {Pid}.", processId);
            return new SourceApplication((int)processId, $"PID_{processId}", null, null);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _concurrencyGate.Dispose();
    }
}

