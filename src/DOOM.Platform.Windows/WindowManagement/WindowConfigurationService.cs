namespace DOOM.Platform.Windows.WindowManagement;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DOOM.Core.Contracts;
using DOOM.Platform.Windows.Native;
using Microsoft.Extensions.Logging;

/// <summary>
/// Native Win32 window management service coordinating overlay styles, non-activating display, and positioning.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowConfigurationService : IWindowConfigurationService
{
    private readonly ILogger<WindowConfigurationService> _logger;

    public WindowConfigurationService(ILogger<WindowConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ApplyOverlayStyles(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var exStyle = Win32Interop.GetWindowLongPtr(hWnd, Win32Interop.GWL_EXSTYLE);
            var newExStyle = new IntPtr(exStyle.ToInt64()
                | Win32Interop.WS_EX_TOPMOST
                | Win32Interop.WS_EX_TOOLWINDOW
                | Win32Interop.WS_EX_NOACTIVATE);

            Win32Interop.SetWindowLongPtr(hWnd, Win32Interop.GWL_EXSTYLE, newExStyle);

            // Re-assert Topmost in Z-order without activating
            Win32Interop.SetWindowPos(
                hWnd,
                Win32Interop.HWND_TOPMOST,
                0, 0, 0, 0,
                Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE);

            _logger.LogDebug("Applied overlay native styles (WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) to HWND {Hwnd}.", hWnd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply native overlay styles to HWND {Hwnd}.", hWnd);
        }
    }

    public void ShowNoActivate(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        // Show window without focus stealing
        Win32Interop.ShowWindow(hWnd, Win32Interop.SW_SHOWNOACTIVATE);

        // Ensure it is on top
        Win32Interop.SetWindowPos(
            hWnd,
            Win32Interop.HWND_TOPMOST,
            0, 0, 0, 0,
            Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE);
    }

    public void Hide(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        Win32Interop.ShowWindow(hWnd, Win32Interop.SW_HIDE);
    }

    public void PositionTopRight(IntPtr hWnd, int width, int height, int margin = 16)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            // 1. Identify the foreground monitor
            var foregroundHwnd = Win32Interop.GetForegroundWindow();
            var targetHwnd = foregroundHwnd != IntPtr.Zero ? foregroundHwnd : hWnd;

            var hMonitor = Win32Interop.MonitorFromWindow(targetHwnd, Win32Interop.MONITOR_DEFAULTTONEAREST);

            var mi = new Win32Interop.MONITORINFO
            {
                cbSize = Marshal.SizeOf<Win32Interop.MONITORINFO>()
            };

            if (Win32Interop.GetMonitorInfo(hMonitor, ref mi))
            {
                var workArea = mi.rcWork;
                var x = workArea.Right - width - margin;
                var y = workArea.Top + margin;

                Win32Interop.SetWindowPos(
                    hWnd,
                    Win32Interop.HWND_TOPMOST,
                    x, y, width, height,
                    Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_SHOWWINDOW);

                _logger.LogDebug("Positioned overlay at ({X}, {Y}) on monitor work area [{WorkArea}]", x, y, workArea.Right);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to position overlay at top-right for HWND {Hwnd}.", hWnd);
        }
    }

    public bool IsTopMost(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        var exStyle = Win32Interop.GetWindowLongPtr(hWnd, Win32Interop.GWL_EXSTYLE);
        return (exStyle.ToInt64() & Win32Interop.WS_EX_TOPMOST) != 0;
    }
}

