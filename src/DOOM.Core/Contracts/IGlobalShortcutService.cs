namespace DOOM.Core.Contracts;

using DOOM.Core.State;

/// <summary>
/// Manages system-wide keyboard shortcuts.
/// </summary>
public interface IGlobalShortcutService
{
    ShortcutStatus Status { get; }
    Task RegisterShortcutsAsync(Func<Task> onTrigger, Func<Task> onToggle, CancellationToken cancellationToken = default);
    Task UnregisterShortcutsAsync();
}

/// <summary>
/// Checks system capabilities and elevation boundaries.
/// </summary>
public interface IPermissionChecker
{
    bool IsRunningAsAdministrator();
    bool IsUiAutomationAvailable();
}

