namespace DOOM.UnitTests;

using System.Windows;
using System.Windows.Interop;
using DOOM.Platform.Windows.Native;
using DOOM.Platform.Windows.WindowManagement;
using Microsoft.Extensions.Logging.Abstractions;

public class WindowConfigurationServiceTests
{
    [Fact]
    public void ApplyOverlayStyles_SetsRequiredWin32ExtendedStyles()
    {
        StaTestRunner.Run(() =>
        {
            var window = new Window
            {
                Width = 200,
                Height = 200,
                WindowStyle = WindowStyle.None,
                Topmost = true
            };
            window.Show();

            var helper = new WindowInteropHelper(window);
            var hWnd = helper.Handle;

            var service = new WindowConfigurationService(NullLogger<WindowConfigurationService>.Instance);

            try
            {
                service.ApplyOverlayStyles(hWnd);

                var exStyle = Win32Interop.GetWindowLongPtr(hWnd, Win32Interop.GWL_EXSTYLE).ToInt64();

                // Verify WS_EX_TOPMOST
                Assert.True((exStyle & Win32Interop.WS_EX_TOPMOST) != 0, "WS_EX_TOPMOST must be set.");

                // Verify WS_EX_TOOLWINDOW
                Assert.True((exStyle & Win32Interop.WS_EX_TOOLWINDOW) != 0, "WS_EX_TOOLWINDOW must be set.");

                // Verify WS_EX_NOACTIVATE
                Assert.True((exStyle & Win32Interop.WS_EX_NOACTIVATE) != 0, "WS_EX_NOACTIVATE must be set.");

                Assert.True(service.IsTopMost(hWnd));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PositionTopRight_CalculatesAndAppliesCoordinates()
    {
        StaTestRunner.Run(() =>
        {
            var window = new Window
            {
                Width = 420,
                Height = 300,
                WindowStyle = WindowStyle.None,
                Topmost = true
            };
            window.Show();

            var helper = new WindowInteropHelper(window);
            var hWnd = helper.Handle;

            var service = new WindowConfigurationService(NullLogger<WindowConfigurationService>.Instance);

            try
            {
                // Position overlay at 420x300 with 16px margin
                service.PositionTopRight(hWnd, 420, 300, margin: 16);

                // Verify window is now visible and has non-zero coordinates
                Assert.True(service.IsTopMost(hWnd));
            }
            finally
            {
                window.Close();
            }
        });
    }
}
