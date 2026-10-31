namespace DOOM.Core.Configuration;

/// <summary>
/// Persisted user preferences for DOOM.
/// Stored securely in %APPDATA%\DOOM\settings.json.
/// </summary>
public sealed record AppSettings
{
    public bool PrivacyEnabled { get; init; } = true;
    public bool ClipboardFallbackEnabled { get; init; } = false;
    public string? ModelIdOverride { get; init; }
    public int PrivacyNoticeAcknowledgedVersion { get; init; } = 0;
    public string TriggerShortcut { get; init; } = "Ctrl+Shift+Space";
    public string ToggleShortcut { get; init; } = "Ctrl+Shift+H";
    public bool StartMinimizedToTray { get; init; } = true;
    public bool HideLocallyFallbackEnabled { get; init; } = false;

    public static readonly AppSettings Default = new();
}

