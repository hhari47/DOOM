namespace DOOM.App;

using System.Windows;
using DOOM.Core.Contracts;
using DOOM.Platform.Windows.SingleInstance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Application entry point and lifecycle coordinator.
/// </summary>
public partial class App : Application
{
    private ISingleInstanceService? _singleInstanceService;
    private ServiceProvider? _serviceProvider;
    private ApplicationCoordinator? _coordinator;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Single-Instance Check
        _singleInstanceService = new SingleInstanceService();
        if (!_singleInstanceService.TryAcquire())
        {
            // Another instance is already active -> Notify existing instance via IPC
            bool notified = await _singleInstanceService.NotifyExistingInstanceAsync(TimeSpan.FromMilliseconds(2000));
            if (!notified)
            {
                // Fallback only if IPC communication could not be established
                MessageBox.Show(
                    "DOOM is already running, but could not be brought to the foreground automatically. Please use Ctrl + Shift + Space or the system tray icon.",
                    "DOOM",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            Shutdown(0);
            return;
        }

        // 2. Build Dependency Injection Container
        var services = new ServiceCollection();
        services.ConfigureServices(_singleInstanceService);
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("DOOM starting up...");

        // 3. Initialize Coordinator
        _coordinator = _serviceProvider.GetRequiredService<ApplicationCoordinator>();
        try
        {
            await _coordinator.InitializeAsync();

            // 4. Start listening for activation requests from subsequent launches
            _singleInstanceService.StartListening(() =>
            {
                logger.LogInformation("External launch signal received. Activating existing overlay window...");
                _coordinator.ActivateFromExternalLaunch();
            });
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Fatal error during application initialization.");
            MessageBox.Show(
                $"Failed to start DOOM: {ex.Message}",
                "DOOM Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_coordinator != null)
        {
            await _coordinator.DisposeAsync();
        }

        if (_singleInstanceService != null)
        {
            await _singleInstanceService.DisposeAsync();
        }

        _serviceProvider?.Dispose();

        base.OnExit(e);
    }
}

