namespace DOOM.App;

using System.Windows.Interop;
using DOOM.Core.Configuration;
using DOOM.Core.Contracts;
using DOOM.Presentation.Views;
using DOOM.Services.Assistant;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates application startup, background coordination, hotkey dispatching,
/// UI overlay lifecycle, and graceful shutdown.
/// </summary>
public sealed class ApplicationCoordinator : IAsyncDisposable
{
    private readonly AppConfiguration _configuration;
    private readonly ISettingsService _settingsService;
    private readonly ICredentialStore _credentialStore;
    private readonly IPrivacyProtectionService _privacyService;
    private readonly IGlobalShortcutService _shortcutService;
    private readonly IWindowConfigurationService _windowConfigService;
    private readonly ISystemTrayCoordinator _trayCoordinator;
    private readonly AssistantController _assistantController;
    private readonly ResponseOverlayWindow _overlayWindow;
    private readonly SettingsWindow _settingsWindow;
    private readonly ILogger<ApplicationCoordinator> _logger;

    public bool IsInitialized { get; private set; }

    public ApplicationCoordinator(
        AppConfiguration configuration,
        ISettingsService settingsService,
        ICredentialStore credentialStore,
        IPrivacyProtectionService privacyService,
        IGlobalShortcutService shortcutService,
        IWindowConfigurationService windowConfigService,
        ISystemTrayCoordinator trayCoordinator,
        AssistantController assistantController,
        ResponseOverlayWindow overlayWindow,
        SettingsWindow settingsWindow,
        ILogger<ApplicationCoordinator> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _privacyService = privacyService ?? throw new ArgumentNullException(nameof(privacyService));
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
        _windowConfigService = windowConfigService ?? throw new ArgumentNullException(nameof(windowConfigService));
        _trayCoordinator = trayCoordinator ?? throw new ArgumentNullException(nameof(trayCoordinator));
        _assistantController = assistantController ?? throw new ArgumentNullException(nameof(assistantController));
        _overlayWindow = overlayWindow ?? throw new ArgumentNullException(nameof(overlayWindow));
        _settingsWindow = settingsWindow ?? throw new ArgumentNullException(nameof(settingsWindow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing DOOM Windows Edition (v{Version})...", _configuration.Version);

        // 1. Load settings
        var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
        _logger.LogInformation("Settings loaded. PrivacyMode={Privacy}, ClipboardFallback={Clipboard}",
            settings.PrivacyEnabled,
            settings.ClipboardFallbackEnabled);

        // 2. Check credentials status (without exposing key content)
        var hasKey = await _credentialStore.HasApiKeyAsync(cancellationToken);
        _logger.LogInformation("Credential status: HasApiKey={HasKey}", hasKey);

        // 3. Pre-create overlay window HWND without activating
        var overlayHwnd = new WindowInteropHelper(_overlayWindow).EnsureHandle();
        _windowConfigService.ApplyOverlayStyles(overlayHwnd);
        _privacyService.ApplyPrivacy(overlayHwnd, settings.PrivacyEnabled);

        // Wire overlay's Settings request to open Settings window
        _overlayWindow.ViewModel.RequestOpenSettings += OpenSettings;

        // 4. Initialize System Tray
        _trayCoordinator.Initialize(new TrayMenuActions
        {
            OnOpenDoom = () =>
            {
                _logger.LogInformation("Tray Action: Open DOOM requested.");
                ShowOverlay();
            },
            OnOpenSettings = () =>
            {
                _logger.LogInformation("Tray Action: Settings requested.");
                OpenSettings();
            },
            OnTogglePanel = () =>
            {
                _logger.LogInformation("Tray Action: Toggle Panel requested.");
                ToggleOverlay();
            },
            OnTogglePrivacy = () =>
            {
                _logger.LogInformation("Tray Action: Toggle Privacy requested.");
                _ = TogglePrivacyAsync();
            },
            OnQuit = () =>
            {
                _logger.LogInformation("Tray Action: Quit DOOM requested.");
                System.Windows.Application.Current.Shutdown(0);
            }
        });

        _trayCoordinator.UpdatePrivacyStatus(settings.PrivacyEnabled);

        // 5. Register Global Shortcuts wired to AssistantController
        await _shortcutService.RegisterShortcutsAsync(
            onTrigger: async () =>
            {
                _logger.LogInformation("Trigger shortcut received -> Dispatching to AssistantController.");
                await _assistantController.HandleTriggerAsync();
            },
            onToggle: () =>
            {
                _logger.LogInformation("Toggle shortcut received.");
                ToggleOverlay();
                return Task.CompletedTask;
            },
            cancellationToken);

        IsInitialized = true;
        _logger.LogInformation("DOOM core native and AI services initialized successfully.");
    }

    /// <summary>
    /// Activates and displays the response overlay in response to an external launch request,
    /// ensuring it is restored from minimized or hidden states and positioned correctly without stealing keyboard focus.
    /// </summary>
    public void ActivateFromExternalLaunch()
    {
        if (System.Windows.Application.Current?.Dispatcher != null &&
            !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(ActivateFromExternalLaunch);
            return;
        }

        try
        {
            _logger.LogInformation("External launch detected: Activating overlay without focus stealing...");

            // 1. Restore from minimized state if needed
            if (_overlayWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _overlayWindow.WindowState = System.Windows.WindowState.Normal;
            }

            // 2. Ensure WPF marks it visible
            if (_overlayWindow.Visibility != System.Windows.Visibility.Visible)
            {
                _overlayWindow.Visibility = System.Windows.Visibility.Visible;
            }

            // 3. Ensure native handle
            var hWnd = new WindowInteropHelper(_overlayWindow).EnsureHandle();
            if (hWnd != IntPtr.Zero)
            {
                // 4. Re-apply overlay styles (WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE)
                _windowConfigService.ApplyOverlayStyles(hWnd);

                // 5. Re-position top-right on the foreground monitor
                var width = _overlayWindow.Width > 0 ? (int)_overlayWindow.Width : 440;
                var height = _overlayWindow.ActualHeight > 0 ? (int)_overlayWindow.ActualHeight : 140;
                _windowConfigService.PositionTopRight(hWnd, width, height);

                // 6. Show using ShowNoActivate (SW_SHOWNOACTIVATE)
                _windowConfigService.ShowNoActivate(hWnd);
            }

            _logger.LogInformation("Overlay window successfully activated without stealing focus.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate overlay from external launch.");
        }
    }

    private void ShowOverlay()
    {
        ActivateFromExternalLaunch();
    }

    private void ToggleOverlay()
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            var hWnd = new WindowInteropHelper(_overlayWindow).Handle;
            if (_overlayWindow.IsVisible)
            {
                _windowConfigService.Hide(hWnd);
            }
            else
            {
                _windowConfigService.ShowNoActivate(hWnd);
            }
        }
        else
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(ToggleOverlay);
        }
    }

    private void OpenSettings()
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }
        else
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(OpenSettings);
        }
    }

    private async Task TogglePrivacyAsync()
    {
        var current = _settingsService.CurrentSettings;
        var next = !current.PrivacyEnabled;
        var updated = current with { PrivacyEnabled = next };
        await _settingsService.SaveSettingsAsync(updated);

        var hWnd = new WindowInteropHelper(_overlayWindow).Handle;
        if (hWnd != IntPtr.Zero)
        {
            _privacyService.ApplyPrivacy(hWnd, next);
        }

        _trayCoordinator.UpdatePrivacyStatus(next);
        _logger.LogInformation("Privacy Mode toggled to {Next}.", next);
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Shutting down DOOM application coordinator...");
        await _shortcutService.UnregisterShortcutsAsync();
        _trayCoordinator.Dispose();
        await _assistantController.DisposeAsync();

        if (System.Windows.Application.Current?.Dispatcher != null && System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            _overlayWindow.Close();
            _settingsWindow.Close();
        }

        IsInitialized = false;
    }
}
