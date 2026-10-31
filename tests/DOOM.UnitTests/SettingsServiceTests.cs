namespace DOOM.UnitTests;

using DOOM.Core.Configuration;
using DOOM.Services.Settings;
using Microsoft.Extensions.Logging.Abstractions;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _settingsFilePath;

    public SettingsServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DOOM_Tests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
        _settingsFilePath = Path.Combine(_tempDirectory, "settings.json");
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileDoesNotExist_ReturnsDefaultSettings()
    {
        var service = new SettingsService(NullLogger<SettingsService>.Instance, _settingsFilePath);

        var settings = await service.LoadSettingsAsync();

        Assert.NotNull(settings);
        Assert.True(settings.PrivacyEnabled);
        Assert.False(settings.ClipboardFallbackEnabled);
        Assert.Equal("Ctrl+Shift+Space", settings.TriggerShortcut);
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsAndLoadsAccurately()
    {
        var service = new SettingsService(NullLogger<SettingsService>.Instance, _settingsFilePath);

        var updated = new AppSettings
        {
            PrivacyEnabled = false,
            ClipboardFallbackEnabled = true,
            ModelIdOverride = "gemini-2.5-flash",
            PrivacyNoticeAcknowledgedVersion = 1,
            TriggerShortcut = "Ctrl+Shift+D"
        };

        await service.SaveSettingsAsync(updated);

        var reloaded = await service.LoadSettingsAsync();

        Assert.False(reloaded.PrivacyEnabled);
        Assert.True(reloaded.ClipboardFallbackEnabled);
        Assert.Equal("gemini-2.5-flash", reloaded.ModelIdOverride);
        Assert.Equal(1, reloaded.PrivacyNoticeAcknowledgedVersion);
        Assert.Equal("Ctrl+Shift+D", reloaded.TriggerShortcut);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileCorrupted_ReturnsSafeDefaults()
    {
        await File.WriteAllTextAsync(_settingsFilePath, "{ invalid json content ... ]]]");

        var service = new SettingsService(NullLogger<SettingsService>.Instance, _settingsFilePath);
        var settings = await service.LoadSettingsAsync();

        Assert.NotNull(settings);
        Assert.Equal(AppSettings.Default.PrivacyEnabled, settings.PrivacyEnabled);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

