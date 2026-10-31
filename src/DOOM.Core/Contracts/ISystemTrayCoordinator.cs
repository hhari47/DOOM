namespace DOOM.Core.Contracts;

public sealed record TrayMenuActions
{
    public Action? OnOpenDoom { get; init; }
    public Action? OnOpenSettings { get; init; }
    public Action? OnTogglePanel { get; init; }
    public Action? OnTogglePrivacy { get; init; }
    public Action? OnQuit { get; init; }
}

/// <summary>
/// Service contract for managing the Windows system notification area icon and context menu.
/// </summary>
public interface ISystemTrayCoordinator : IDisposable
{
    void Initialize(TrayMenuActions actions);
    void UpdatePrivacyStatus(bool enabled);
    void UpdateStatusText(string status);
    void ShowNotification(string title, string message);
}

