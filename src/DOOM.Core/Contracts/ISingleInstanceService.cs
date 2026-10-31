namespace DOOM.Core.Contracts;

/// <summary>
/// Manages single-instance enforcement and inter-process communication (IPC)
/// to notify an existing application instance when a subsequent launch occurs.
/// </summary>
public interface ISingleInstanceService : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets whether this process instance successfully acquired primary ownership.
    /// </summary>
    bool IsFirstInstance { get; }

    /// <summary>
    /// Attempts to acquire the single-instance lock.
    /// </summary>
    /// <returns>True if this is the first running instance; false if an instance is already active.</returns>
    bool TryAcquire();

    /// <summary>
    /// Starts listening for IPC activation requests from subsequent instances.
    /// When a signal is received, <paramref name="onActivated"/> is executed.
    /// </summary>
    /// <param name="onActivated">Callback invoked upon receiving an activation request.</param>
    void StartListening(Action onActivated);

    /// <summary>
    /// Notifies the running primary instance to activate and display its interface.
    /// </summary>
    /// <param name="timeout">Maximum duration to await pipe connection and message delivery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the notification was delivered successfully; false otherwise.</returns>
    Task<bool> NotifyExistingInstanceAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

