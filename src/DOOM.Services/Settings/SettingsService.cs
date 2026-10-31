namespace DOOM.Services.Settings;

using System.Text.Json;
using DOOM.Core.Configuration;
using DOOM.Core.Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Persists application preferences to %APPDATA%\DOOM\settings.json with atomic write guarantees.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private readonly ILogger<SettingsService> _logger;
    private readonly object _lock = new();
    private AppSettings _currentSettings = AppSettings.Default;

    public AppSettings CurrentSettings
    {
        get
        {
            lock (_lock)
            {
                return _currentSettings;
            }
        }
    }

    public SettingsService(ILogger<SettingsService> logger, string? customFilePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _settingsFilePath = customFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var doomFolder = Path.Combine(appData, "DOOM");
            Directory.CreateDirectory(doomFolder);
            _settingsFilePath = Path.Combine(doomFolder, "settings.json");
        }
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            _logger.LogInformation("No settings file found at '{Path}'. Initializing with defaults.", _settingsFilePath);
            lock (_lock)
            {
                _currentSettings = AppSettings.Default;
                return _currentSettings;
            }
        }

        try
        {
            await using var stream = File.OpenRead(_settingsFilePath);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);

            lock (_lock)
            {
                _currentSettings = loaded ?? AppSettings.Default;
                return _currentSettings;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize settings from '{Path}'. Reverting to safe defaults.", _settingsFilePath);
            lock (_lock)
            {
                _currentSettings = AppSettings.Default;
                return _currentSettings;
            }
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var tempPath = _settingsFilePath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            }

            File.Move(tempPath, _settingsFilePath, overwrite: true);

            lock (_lock)
            {
                _currentSettings = settings;
            }

            _logger.LogInformation("Settings saved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to '{Path}'.", _settingsFilePath);
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }
            throw;
        }
    }
}

