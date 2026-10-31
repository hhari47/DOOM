namespace DOOM.Platform.Windows.Shortcuts;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Interop;
using DOOM.Core.Contracts;
using DOOM.Core.State;
using DOOM.Platform.Windows.Native;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registers and monitors global keyboard hotkeys using Win32 RegisterHotKey with a dedicated message hook.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GlobalShortcutService : IGlobalShortcutService, IDisposable
{
    private const int TriggerHotKeyId = 9001;
    private const int ToggleHotKeyId = 9002;

    private readonly ILogger<GlobalShortcutService> _logger;
    private HwndSource? _hwndSource;
    private Func<Task>? _onTrigger;
    private Func<Task>? _onToggle;
    private ShortcutStatus _status = new();
    private bool _isDisposed;

    public ShortcutStatus Status => _status;

    public GlobalShortcutService(ILogger<GlobalShortcutService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task RegisterShortcutsAsync(
        Func<Task> onTrigger,
        Func<Task> onToggle,
        CancellationToken cancellationToken = default)
    {
        _onTrigger = onTrigger ?? throw new ArgumentNullException(nameof(onTrigger));
        _onToggle = onToggle ?? throw new ArgumentNullException(nameof(onToggle));

        // Create message-only window to receive WM_HOTKEY
        var parameters = new HwndSourceParameters("DOOM_HotKey_MessageReceiver")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            ParentWindow = IntPtr.Zero
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(HwndHook);

        var hWnd = _hwndSource.Handle;
        var modifiers = Win32Interop.MOD_CONTROL | Win32Interop.MOD_SHIFT | Win32Interop.MOD_NOREPEAT;

        // 1. Register Trigger: Ctrl + Shift + Space
        var triggerSuccess = Win32Interop.RegisterHotKey(hWnd, TriggerHotKeyId, modifiers, Win32Interop.VK_SPACE);
        var triggerError = triggerSuccess ? 0 : Marshal.GetLastWin32Error();

        // 2. Register Toggle: Ctrl + Shift + H
        var toggleSuccess = Win32Interop.RegisterHotKey(hWnd, ToggleHotKeyId, modifiers, Win32Interop.VK_H);
        var toggleError = toggleSuccess ? 0 : Marshal.GetLastWin32Error();

        var triggerState = triggerSuccess
            ? ShortcutRegistrationState.Active
            : (triggerError == 1409 ? ShortcutRegistrationState.Conflict : ShortcutRegistrationState.Failed);

        var toggleState = toggleSuccess
            ? ShortcutRegistrationState.Active
            : (toggleError == 1409 ? ShortcutRegistrationState.Conflict : ShortcutRegistrationState.Failed);

        _status = new ShortcutStatus
        {
            TriggerState = triggerState,
            ToggleState = toggleState,
            TriggerDescription = "Ctrl + Shift + Space",
            ToggleDescription = "Ctrl + Shift + H",
            LastError = (!triggerSuccess || !toggleSuccess)
                ? $"TriggerErr={triggerError}, ToggleErr={toggleError}"
                : null
        };

        if (triggerSuccess && toggleSuccess)
        {
            _logger.LogInformation("Global shortcuts registered successfully: Trigger=Ctrl+Shift+Space, Toggle=Ctrl+Shift+H");
        }
        else
        {
            _logger.LogWarning("Shortcut registration issue: {Status}", _status);
        }

        return Task.CompletedTask;
    }

    public Task UnregisterShortcutsAsync()
    {
        if (_hwndSource != null && _hwndSource.Handle != IntPtr.Zero)
        {
            Win32Interop.UnregisterHotKey(_hwndSource.Handle, TriggerHotKeyId);
            Win32Interop.UnregisterHotKey(_hwndSource.Handle, ToggleHotKeyId);

            _status = _status with
            {
                TriggerState = ShortcutRegistrationState.Unregistered,
                ToggleState = ShortcutRegistrationState.Unregistered
            };

            _logger.LogInformation("Global shortcuts unregistered.");
        }

        return Task.CompletedTask;
    }

    private IntPtr HwndHook(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Interop.WM_HOTKEY)
        {
            var hotKeyId = wParam.ToInt32();

            if (hotKeyId == TriggerHotKeyId)
            {
                _logger.LogDebug("Trigger hotkey received (Ctrl + Shift + Space).");
                if (_onTrigger != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try { await _onTrigger(); }
                        catch (Exception ex) { _logger.LogError(ex, "Error handling trigger hotkey."); }
                    });
                }
                handled = true;
            }
            else if (hotKeyId == ToggleHotKeyId)
            {
                _logger.LogDebug("Toggle hotkey received (Ctrl + Shift + H).");
                if (_onToggle != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try { await _onToggle(); }
                        catch (Exception ex) { _logger.LogError(ex, "Error handling toggle hotkey."); }
                    });
                }
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        UnregisterShortcutsAsync().GetAwaiter().GetResult();

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
    }
}

