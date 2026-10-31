namespace DOOM.UnitTests;

using System.Windows;
using System.Windows.Interop;
using DOOM.Core.State;
using DOOM.Platform.Windows.Native;
using DOOM.Platform.Windows.Privacy;
using Microsoft.Extensions.Logging.Abstractions;

public class PrivacyProtectionServiceTests
{
    [Fact]
    public void ApplyPrivacy_OnValidHwnd_AppliesAndVerifiesAffinity()
    {
        StaTestRunner.Run(() =>
        {
            var window = new Window
            {
                Width = 100,
                Height = 100,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false
            };

            var helper = new WindowInteropHelper(window);
            helper.EnsureHandle();
            var hWnd = helper.Handle;

            Assert.NotEqual(IntPtr.Zero, hWnd);

            var service = new PrivacyProtectionService(NullLogger<PrivacyProtectionService>.Instance);

            try
            {
                // 1. Initial State
                Assert.False(service.CurrentState.IsRequested);
                Assert.False(service.CurrentState.IsApplied);

                // 2. Enable Privacy Mode
                var applied = service.ApplyPrivacy(hWnd, enable: true);
                Assert.True(applied);
                Assert.True(service.CurrentState.IsRequested);
                Assert.True(service.CurrentState.IsApplied);
                Assert.Equal(PrivacyVerificationStatus.SelfTestPassed, service.CurrentState.Verification);
                Assert.True(service.CurrentState.IsProtectionActive);

                // Direct native verification
                var getSuccess = Win32Interop.GetWindowDisplayAffinity(hWnd, out var affinity);
                Assert.True(getSuccess);
                Assert.Equal(Win32Interop.WDA_EXCLUDEFROMCAPTURE, affinity);

                // 3. Disable Privacy Mode
                var disabled = service.ApplyPrivacy(hWnd, enable: false);
                Assert.True(disabled);
                Assert.False(service.CurrentState.IsRequested);
                Assert.False(service.CurrentState.IsApplied);

                Win32Interop.GetWindowDisplayAffinity(hWnd, out var affinityNone);
                Assert.Equal(Win32Interop.WDA_NONE, affinityNone);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ApplyPrivacy_WithZeroHwnd_ReturnsFalse()
    {
        var service = new PrivacyProtectionService(NullLogger<PrivacyProtectionService>.Instance);

        var result = service.ApplyPrivacy(IntPtr.Zero, enable: true);

        Assert.False(result);
        Assert.True(service.CurrentState.IsRequested);
        Assert.False(service.CurrentState.IsApplied);
        Assert.NotNull(service.CurrentState.StatusReason);
    }

    [Fact]
    public void UpdateSharingContext_ChangesEffectiveStatusHonourably()
    {
        StaTestRunner.Run(() =>
        {
            var window = new Window { Width = 50, Height = 50, WindowStyle = WindowStyle.None };
            var helper = new WindowInteropHelper(window);
            helper.EnsureHandle();
            var hWnd = helper.Handle;

            var service = new PrivacyProtectionService(NullLogger<PrivacyProtectionService>.Instance);

            try
            {
                service.ApplyPrivacy(hWnd, enable: true);
                Assert.Equal(ProtectionStatus.SelfTestPassed, service.CurrentState.EffectiveStatus);

                // User switches to Entire Screen sharing in a meeting tool
                service.UpdateSharingContext(SharingContext.EntireScreen);
                Assert.Equal(ProtectionStatus.KnownNotEffective, service.CurrentState.EffectiveStatus);

                // User switches to Single Window sharing
                service.UpdateSharingContext(SharingContext.SingleWindow);
                Assert.Equal(ProtectionStatus.SelfTestPassed, service.CurrentState.EffectiveStatus);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
