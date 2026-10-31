namespace DOOM.Core.State;

public enum ShortcutRegistrationState
{
    Active,
    Conflict,
    Failed,
    Unregistered
}

public sealed record ShortcutStatus
{
    public ShortcutRegistrationState TriggerState { get; init; } = ShortcutRegistrationState.Unregistered;
    public ShortcutRegistrationState ToggleState { get; init; } = ShortcutRegistrationState.Unregistered;
    public string TriggerDescription { get; init; } = "Ctrl + Shift + Space";
    public string ToggleDescription { get; init; } = "Ctrl + Shift + H";
    public string? LastError { get; init; }

    public bool IsOperational => TriggerState == ShortcutRegistrationState.Active;
}

