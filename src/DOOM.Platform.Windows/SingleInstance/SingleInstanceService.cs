namespace DOOM.Platform.Windows.SingleInstance;

using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DOOM.Core.Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Enforces single-instance application execution using a Win32 Mutex and provides
/// reliable Named Pipe IPC to bring the existing instance's overlay to the foreground on subsequent launches.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SingleInstanceService : ISingleInstanceService
{
    private const string DefaultMutexName = @"Global\DOOM_Windows_SingleInstance_Mutex";
    private static readonly string DefaultPipeName = $"DOOM_Windows_SingleInstance_Pipe_{Environment.UserName}";
    private const string ActivationCommand = "ACTIVATE";

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly ILogger<SingleInstanceService>? _logger;

    private Mutex? _mutex;
    private bool _hasHandle;
    private CancellationTokenSource? _listenCts;
    private Task? _listenTask;
    private bool _isDisposed;

    public bool IsFirstInstance => _hasHandle;

    public SingleInstanceService(
        string? mutexName = null,
        string? pipeName = null,
        ILogger<SingleInstanceService>? logger = null)
    {
        _mutexName = !string.IsNullOrWhiteSpace(mutexName) ? mutexName : DefaultMutexName;
        _pipeName = !string.IsNullOrWhiteSpace(pipeName) ? pipeName : DefaultPipeName;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        try
        {
            _mutex = new Mutex(initiallyOwned: true, _mutexName, out _hasHandle);
            if (_hasHandle)
            {
                _logger?.LogInformation("Successfully acquired single-instance mutex '{MutexName}'.", _mutexName);
                return true;
            }

            // If the mutex already existed, test whether it is unowned or abandoned
            try
            {
                if (_mutex.WaitOne(0, false))
                {
                    _hasHandle = true;
                    _logger?.LogInformation("Acquired unowned existing single-instance mutex '{MutexName}'.", _mutexName);
                    return true;
                }
            }
            catch (AbandonedMutexException ex)
            {
                _logger?.LogWarning(ex, "Acquired abandoned single-instance mutex '{MutexName}'. Previous instance terminated abnormally.", _mutexName);
                _hasHandle = true;
                return true;
            }

            _logger?.LogInformation("Single-instance mutex '{MutexName}' is already owned by another process.", _mutexName);
            return false;
        }
        catch (AbandonedMutexException ex)
        {
            // Previous owner crashed or terminated abnormally without releasing the mutex
            _logger?.LogWarning(ex, "Acquired abandoned single-instance mutex '{MutexName}'. Previous instance terminated abnormally.", _mutexName);
            _hasHandle = true;
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.LogWarning(ex, "Access denied to mutex '{MutexName}'. Attempting local session fallback...", _mutexName);
            try
            {
                var fallbackMutexName = $@"Local\DOOM_Windows_SingleInstance_Mutex_{Environment.UserName}";
                _mutex = new Mutex(initiallyOwned: true, fallbackMutexName, out _hasHandle);
                if (_hasHandle) return true;

                try
                {
                    if (_mutex.WaitOne(0, false))
                    {
                        _hasHandle = true;
                        return true;
                    }
                }
                catch (AbandonedMutexException)
                {
                    _hasHandle = true;
                    return true;
                }

                return false;
            }
            catch (Exception fallbackEx)
            {
                _logger?.LogError(fallbackEx, "Failed to acquire fallback single-instance mutex.");
                _hasHandle = false;
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error acquiring single-instance mutex '{MutexName}'.", _mutexName);
            _hasHandle = false;
            return false;
        }
    }

    /// <inheritdoc />
    public void StartListening(Action onActivated)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(onActivated);

        if (!_hasHandle)
        {
            throw new InvalidOperationException("Cannot start IPC listener on a non-primary application instance.");
        }

        if (_listenTask != null)
        {
            return; // Already listening
        }

        _listenCts = new CancellationTokenSource();
        var token = _listenCts.Token;

        _listenTask = Task.Run(async () =>
        {
            _logger?.LogInformation("Single-instance IPC server listening on named pipe '{PipeName}'...", _pipeName);

            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: false);
                    var message = await reader.ReadLineAsync(token).ConfigureAwait(false);

                    if (string.Equals(message?.Trim(), ActivationCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogInformation("Received '{Command}' signal via IPC pipe. Triggering activation callback...", ActivationCommand);
                        try
                        {
                            onActivated();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error occurred while executing activation callback.");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    _logger?.LogWarning(ex, "IPC server pipe encounter cycle error. Re-establishing listener...");
                    try
                    {
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                finally
                {
                    server?.Dispose();
                }
            }

            _logger?.LogInformation("Single-instance IPC server stopped.");
        }, token);
    }

    /// <inheritdoc />
    public async Task<bool> NotifyExistingInstanceAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        try
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            _logger?.LogInformation("Connecting to primary instance on pipe '{PipeName}'...", _pipeName);

            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(linkedCts.Token).ConfigureAwait(false);

            using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: false)
            {
                AutoFlush = true
            };

            await writer.WriteLineAsync(ActivationCommand.AsMemory(), linkedCts.Token).ConfigureAwait(false);
            await client.FlushAsync(linkedCts.Token).ConfigureAwait(false);

            _logger?.LogInformation("Successfully sent activation notification to existing DOOM instance.");
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Timed out or cancelled while awaiting connection to primary DOOM pipe '{PipeName}'.", _pipeName);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to connect or deliver activation signal via pipe '{PipeName}'.", _pipeName);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_listenCts != null)
        {
            _listenCts.Cancel();
            if (_listenTask != null)
            {
                try
                {
                    await _listenTask.ConfigureAwait(false);
                }
                catch
                {
                    // Ignore cancellation errors during shutdown
                }
            }

            _listenCts.Dispose();
            _listenCts = null;
        }

        ReleaseMutexSafely();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_listenCts != null)
        {
            _listenCts.Cancel();
            _listenCts.Dispose();
            _listenCts = null;
        }

        ReleaseMutexSafely();
    }

    private void ReleaseMutexSafely()
    {
        if (_hasHandle && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Notice: Error releasing single-instance mutex.");
            }
            finally
            {
                _hasHandle = false;
            }
        }

        _mutex?.Dispose();
        _mutex = null;
    }
}
