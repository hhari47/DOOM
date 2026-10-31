namespace DOOM.Presentation.Views;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DOOM.Presentation.ViewModels;

/// <summary>
/// Settings window implementing the 5-card layout: setup checklist, Gemini credentials &amp; model,
/// shortcuts, native privacy options, and about information.
/// </summary>
public partial class SettingsWindow : Window
{
    private bool _isSyncing;

    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        if (ApiKeyPasswordBox != null) ApiKeyPasswordBox.Password = ViewModel.ApiKeyInput ?? string.Empty;
        if (ApiKeyTextBox != null) ApiKeyTextBox.Text = ViewModel.ApiKeyInput ?? string.Empty;

        Loaded += async (_, _) => await ViewModel.InitializeAsync();
        PreviewKeyDown += (sender, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        };

        ViewModel.RequestQuit += () => Application.Current.Shutdown(0);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.ApiKeyInput))
        {
            if (_isSyncing) return;
            _isSyncing = true;
            try
            {
                var val = ViewModel.ApiKeyInput ?? string.Empty;
                if (ApiKeyPasswordBox != null && ApiKeyPasswordBox.Password != val)
                {
                    ApiKeyPasswordBox.Password = val;
                }
                if (ApiKeyTextBox != null && ApiKeyTextBox.Text != val)
                {
                    ApiKeyTextBox.Text = val;
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }
        else if (e.PropertyName == nameof(ViewModel.IsKeyRevealed))
        {
            _isSyncing = true;
            try
            {
                var val = ViewModel.ApiKeyInput ?? string.Empty;
                if (ViewModel.IsKeyRevealed)
                {
                    if (ApiKeyTextBox != null)
                    {
                        if (ApiKeyTextBox.Text != val)
                        {
                            ApiKeyTextBox.Text = val;
                        }
                        ApiKeyTextBox.Focus();
                        ApiKeyTextBox.CaretIndex = ApiKeyTextBox.Text.Length;
                    }
                }
                else
                {
                    if (ApiKeyPasswordBox != null)
                    {
                        if (ApiKeyPasswordBox.Password != val)
                        {
                            ApiKeyPasswordBox.Password = val;
                        }
                        ApiKeyPasswordBox.Focus();
                    }
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Settings window should not terminate the application; hide instead
        e.Cancel = true;
        Hide();
    }

    private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        try
        {
            if (sender is PasswordBox pb)
            {
                var pwd = pb.Password;
                ViewModel.ApiKeyInput = pwd;
                if (ApiKeyTextBox != null && ApiKeyTextBox.Text != pwd)
                {
                    ApiKeyTextBox.Text = pwd;
                }
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        try
        {
            if (sender is TextBox tb)
            {
                var text = tb.Text;
                ViewModel.ApiKeyInput = text;
                if (ApiKeyPasswordBox != null && ApiKeyPasswordBox.Password != text)
                {
                    ApiKeyPasswordBox.Password = text;
                }
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }
}

