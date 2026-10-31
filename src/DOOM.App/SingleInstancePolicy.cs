namespace DOOM.App;

using DOOM.Platform.Windows.SingleInstance;

/// <summary>
/// Enforces single-instance execution via a named Win32 Mutex and Named Pipe IPC.
/// Prevents multiple copies of DOOM from running simultaneously and creating hotkey conflicts.
/// </summary>
public sealed class SingleInstancePolicy : IDisposable
{
    private readonly SingleInstanceService _service = new();

    public bool IsOnlyInstance => _service.IsFirstInstance;

    public bool TryAcquire() => _service.TryAcquire();

    public void Dispose() => _service.Dispose();
}

