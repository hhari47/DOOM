namespace DOOM.Platform.Windows.Clipboard;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using DOOM.Core.Domain;
using DOOM.Core.Errors;
using DOOM.Platform.Windows.Native;
using Microsoft.Extensions.Logging;

/// <summary>
/// Safe clipboard-based text acquisition fallback.
/// Enforces opt-in gating, sequence-number monitoring, bounded timeouts, and prevents overwriting newer clipboard data.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ClipboardFallbackHandler
{
    private const int PollingIntervalMs = 20;
    private const int MaxPollingAttempts = 30; // 30 * 20ms = 600ms total bounded wait

    private readonly ILogger<ClipboardFallbackHandler> _logger;

    public ClipboardFallbackHandler(ILogger<ClipboardFallbackHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes clipboard fallback acquisition on a dedicated STA thread.
    /// </summary>
    public async Task<SelectedText> AcquireViaClipboardAsync(
        SourceApplication sourceApp,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<SelectedText>(TaskCreationOptions.RunContinuationsAsynchronously);

        var staThread = new Thread(() =>
        {
            try
            {
                var result = ExecuteClipboardCapture(sourceApp);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            Name = "DOOM_Clipboard_Fallback_Worker",
            IsBackground = true
        };

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linkedCts.Token));
        if (completed == tcs.Task)
        {
            return await tcs.Task;
        }

        if (timeoutCts.IsCancellationRequested)
        {
            throw new TextAcquisitionException(TextAcquisitionError.TimedOut, "Clipboard fallback operation timed out.");
        }

        throw new TextAcquisitionException(TextAcquisitionError.Cancelled, "Clipboard fallback cancelled.");
    }

    private SelectedText ExecuteClipboardCapture(SourceApplication sourceApp)
    {
        // 1. Initial State & Sequence Number
        var initialSeq = Win32Interop.GetClipboardSequenceNumber();

        // 2. Snapshot existing clipboard content
        IDataObject? clipboardBackup = null;
        try
        {
            clipboardBackup = System.Windows.Clipboard.GetDataObject();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not snapshot initial clipboard contents.");
        }

        // 3. Synthesize Ctrl+C
        SimulateCtrlC();

        // 4. Poll for clipboard change up to 600ms
        uint newSeq = initialSeq;
        for (var attempt = 0; attempt < MaxPollingAttempts; attempt++)
        {
            Thread.Sleep(PollingIntervalMs);
            newSeq = Win32Interop.GetClipboardSequenceNumber();
            if (newSeq != initialSeq)
            {
                break;
            }
        }

        if (newSeq == initialSeq)
        {
            _logger.LogWarning("No clipboard change detected after Ctrl+C simulation within 600ms.");
            throw new TextAcquisitionException(TextAcquisitionError.EmptySelection, "No text selection was captured via clipboard.");
        }

        // 5. Read captured text
        string? capturedText = null;
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                capturedText = System.Windows.Clipboard.GetText();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read copied text from clipboard.");
            throw new TextAcquisitionException(TextAcquisitionError.ClipboardCopyFailed, "Failed to read captured clipboard text.", ex);
        }

        // 6. Sequence Number Verification before Restoration
        var postReadSeq = Win32Interop.GetClipboardSequenceNumber();
        var restoreSkipped = false;

        if (postReadSeq != newSeq)
        {
            // Another process or user action placed newer data on the clipboard!
            _logger.LogInformation("Newer clipboard data detected (Seq {PostSeq} != {NewSeq}). Skipping restoration to preserve user data.", postReadSeq, newSeq);
            restoreSkipped = true;
        }
        else if (clipboardBackup != null)
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(clipboardBackup, copy: false);
                _logger.LogDebug("Successfully restored previous clipboard contents.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore previous clipboard contents.");
                restoreSkipped = true;
            }
        }

        return SelectionValidator.Validate(capturedText, sourceApp, AcquisitionMethod.ClipboardFallback, restoreSkipped);
    }

    private static void SimulateCtrlC()
    {
        var inputs = new Win32Interop.INPUT[4];

        // 1. Ctrl Down
        inputs[0].type = Win32Interop.INPUT_KEYBOARD;
        inputs[0].ki.wVk = (ushort)Win32Interop.VK_CONTROL;

        // 2. C Down
        inputs[1].type = Win32Interop.INPUT_KEYBOARD;
        inputs[1].ki.wVk = (ushort)Win32Interop.VK_C;

        // 3. C Up
        inputs[2].type = Win32Interop.INPUT_KEYBOARD;
        inputs[2].ki.wVk = (ushort)Win32Interop.VK_C;
        inputs[2].ki.dwFlags = Win32Interop.KEYEVENTF_KEYUP;

        // 4. Ctrl Up
        inputs[3].type = Win32Interop.INPUT_KEYBOARD;
        inputs[3].ki.wVk = (ushort)Win32Interop.VK_CONTROL;
        inputs[3].ki.dwFlags = Win32Interop.KEYEVENTF_KEYUP;

        var sent = Win32Interop.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32Interop.INPUT>());
        if (sent != inputs.Length)
        {
            var err = Marshal.GetLastWin32Error();
            throw new TextAcquisitionException(TextAcquisitionError.ClipboardCopyFailed, $"SendInput failed with Win32 error {err}.");
        }
    }
}

