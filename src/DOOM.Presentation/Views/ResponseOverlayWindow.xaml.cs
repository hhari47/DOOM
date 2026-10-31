namespace DOOM.Presentation.Views;

using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DOOM.Core.Contracts;
using DOOM.Presentation.ViewModels;

/// <summary>
/// Floating, topmost, non-activating response overlay window.
/// Excludes itself from display capture when Privacy Mode is active.
/// </summary>
public partial class ResponseOverlayWindow : Window
{
    private readonly IWindowConfigurationService _windowConfigService;
    private readonly IPrivacyProtectionService _privacyService;
    private readonly ISettingsService _settingsService;
    private IntPtr _windowHandle = IntPtr.Zero;

    public ResponseOverlayViewModel ViewModel { get; }

    public ResponseOverlayWindow(
        ResponseOverlayViewModel viewModel,
        IWindowConfigurationService windowConfigService,
        IPrivacyProtectionService privacyService,
        ISettingsService settingsService)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _windowConfigService = windowConfigService ?? throw new ArgumentNullException(nameof(windowConfigService));
        _privacyService = privacyService ?? throw new ArgumentNullException(nameof(privacyService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        DataContext = ViewModel;
        InitializeComponent();

        ViewModel.RequestHide += OnRequestHide;
        ViewModel.RequestShow += OnRequestShow;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _windowHandle = new WindowInteropHelper(this).Handle;
        if (_windowHandle != IntPtr.Zero)
        {
            // 1. Apply floating, tool-window, non-activating styles
            _windowConfigService.ApplyOverlayStyles(_windowHandle);

            // 2. Apply display capture exclusion affinity if privacy mode is enabled
            var privacyEnabled = _settingsService.CurrentSettings.PrivacyEnabled;
            _privacyService.ApplyPrivacy(_windowHandle, privacyEnabled);

            // 3. Position top-right on primary work area
            _windowConfigService.PositionTopRight(_windowHandle, (int)Width, (int)ActualHeight);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Never destroy the overlay window during normal execution; hide it instead
        e.Cancel = true;
        OnRequestHide();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnRequestHide()
    {
        if (Dispatcher.CheckAccess())
        {
            Visibility = Visibility.Hidden;
            if (_windowHandle != IntPtr.Zero)
            {
                _windowConfigService.Hide(_windowHandle);
            }
        }
        else
        {
            Dispatcher.InvokeAsync(OnRequestHide);
        }
    }

    private void OnRequestShow()
    {
        if (Dispatcher.CheckAccess())
        {
            Visibility = Visibility.Visible;
            if (_windowHandle != IntPtr.Zero)
            {
                _windowConfigService.ShowNoActivate(_windowHandle);
            }
        }
        else
        {
            Dispatcher.InvokeAsync(OnRequestShow);
        }
    }
}

