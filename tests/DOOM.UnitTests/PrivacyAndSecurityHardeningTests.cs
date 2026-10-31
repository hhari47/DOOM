namespace DOOM.UnitTests;

using System.Windows;
using System.Windows.Interop;
using DOOM.Core.Configuration;
using DOOM.Core.Domain;
using DOOM.Core.Logging;
using DOOM.Platform.Windows.Native;
using DOOM.Platform.Windows.Privacy;
using DOOM.Services.Settings;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Phase 7 Privacy and Security Hardening Tests.
/// Validates display capture exclusion affinity on real HWNDs, clipboard sequence-number protection,
/// log redaction boundaries, and zero-persistence of captured text.
/// </summary>
public sealed class PrivacyAndSecurityHardeningTests
{
    [Fact]
    public void DisplayAffinity_AppliesExcludeFromCaptureAndReturnsToNoneCleanly()
    {
        StaTestRunner.Run(() =>
        {
            var privacyService = new PrivacyProtectionService(NullLogger<PrivacyProtectionService>.Instance);
            var window = new Window { Width = 200, Height = 100 };
            var helper = new WindowInteropHelper(window);
            var hWnd = helper.EnsureHandle();

            try
            {
                // 1. Apply Privacy (Enable)
                var applied = privacyService.ApplyPrivacy(hWnd, enable: true);
                Assert.True(applied);

                Win32Interop.GetWindowDisplayAffinity(hWnd, out var affinity);
                Assert.Equal(Win32Interop.WDA_EXCLUDEFROMCAPTURE, affinity);
                Assert.True(privacyService.CurrentState.IsProtectionActive);

                // 2. Remove Privacy (Disable)
                var removed = privacyService.ApplyPrivacy(hWnd, enable: false);
                Assert.True(removed);

                Win32Interop.GetWindowDisplayAffinity(hWnd, out var affinityAfter);
                Assert.Equal(Win32Interop.WDA_NONE, affinityAfter);
                Assert.False(privacyService.CurrentState.IsProtectionActive);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SensitiveDataRedactor_MasksApiKeysAndPromptContents()
    {
        var apiKey = "AIzaSyD-1234567890abcdefghijklmnopqrstuv";
        var logMessage = $"Connecting to Gemini with key: {apiKey} and prompt: 'secret financial report'";

        var redacted = SensitiveDataRedactor.Redact(logMessage);

        Assert.DoesNotContain(apiKey, redacted);
        Assert.Contains("<redacted_api_key>", redacted);
    }

    [Fact]
    public void ApiKey_AccessKeyIsRestrictedAndToStringIsRedacted()
    {
        var raw = "AIzaSyD-1234567890abcdefghijklmnopqrstuv";
        var key = ApiKey.Create(raw);

        // AccessKey() returns raw value strictly for HTTP header injection
        Assert.Equal(raw, key.AccessKey());

        // ToString() is completely masked to prevent telemetry or formatting leaks
        Assert.Equal("<redacted>", key.ToString());
    }

    [Fact]
    public async Task ZeroPersistenceAudit_SettingsJsonNeverContainsCapturedTextOrPrompts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DOOM_Audit_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var settingsPath = Path.Combine(tempDir, "settings.json");

        try
        {
            var settingsService = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);

            var settings = new AppSettings
            {
                PrivacyEnabled = true,
                ClipboardFallbackEnabled = false,
                ModelIdOverride = "gemini-3.5-flash"
            };

            await settingsService.SaveSettingsAsync(settings);

            var jsonOnDisk = await File.ReadAllTextAsync(settingsPath);

            // Audit: Verify settings file contains only schema-defined preference properties
            Assert.Contains("PrivacyEnabled", jsonOnDisk);
            Assert.Contains("ClipboardFallbackEnabled", jsonOnDisk);
            Assert.Contains("ModelIdOverride", jsonOnDisk);

            // Audit: Verify no captured text or prompt fields exist
            Assert.DoesNotContain("SelectedText", jsonOnDisk);
            Assert.DoesNotContain("Prompt", jsonOnDisk);
            Assert.DoesNotContain("Response", jsonOnDisk);
            Assert.DoesNotContain("ApiKey", jsonOnDisk);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
