namespace DOOM.UnitTests;

using System.Windows;
using DOOM.Core.Configuration;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.State;
using DOOM.Presentation.Controls;
using DOOM.Presentation.ViewModels;
using DOOM.Presentation.Views;
using DOOM.Services.Assistant;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class WindowInstantiationTests
{
    private static readonly SourceApplication TestApp = new(1, "TestApp", "Title", "path");
    private static readonly SelectedText ValidSelection = new("Selected text", TestApp, AcquisitionMethod.UiAutomation);
    private static readonly ApiKey ValidApiKey = ApiKey.Create("AIzaSyD-1234567890abcdefghijklmnopqrstuv");

    [Fact]
    public void IncrementalMarkdownViewer_InstantiatesAndRendersCorrectly()
    {
        StaTestRunner.Run(() =>
        {
            EnsureApplicationResources();

            var viewer = new IncrementalMarkdownViewer();
            viewer.UpdateContent("Hello **world**, check [link](https://test.com)!");

            Assert.NotNull(viewer.Document);
            Assert.NotEmpty(viewer.Document.Blocks);
        });
    }

    [Fact]
    public void ResponseOverlayWindow_InstantiatesWithViewModel()
    {
        StaTestRunner.Run(() =>
        {
            EnsureApplicationResources();

            var (vm, _) = CreateOverlayViewModel();
            var windowConfig = new MockWindowConfig();
            var privacy = new MockPrivacy();
            var settings = new MockSettings();

            var window = new ResponseOverlayWindow(vm, windowConfig, privacy, settings);

            Assert.NotNull(window);
            Assert.Equal(vm, window.DataContext);
            Assert.Equal(440, window.Width);

            window.Close();
        });
    }

    [Fact]
    public void SettingsWindow_InstantiatesWithViewModel()
    {
        StaTestRunner.Run(() =>
        {
            EnsureApplicationResources();

            var vm = CreateSettingsViewModel();
            var window = new SettingsWindow(vm);

            Assert.NotNull(window);
            Assert.Equal(vm, window.DataContext);

            window.Close();
        });
    }

    [Fact]
    public void SettingsWindow_PasswordBox_And_TextBox_SyncValues_OnVisibilityToggle()
    {
        StaTestRunner.Run(() =>
        {
            EnsureApplicationResources();

            var vm = CreateSettingsViewModel();
            var window = new SettingsWindow(vm);

            // 1. Initial state
            Assert.False(vm.IsKeyRevealed);
            Assert.Equal(string.Empty, window.ApiKeyPasswordBox.Password);
            Assert.Equal(string.Empty, window.ApiKeyTextBox.Text);

            // 2. Setting ViewModel input updates both controls
            vm.ApiKeyInput = "AIzaSyD-sampleKey1234567890abcdef";
            Assert.Equal("AIzaSyD-sampleKey1234567890abcdef", window.ApiKeyPasswordBox.Password);
            Assert.Equal("AIzaSyD-sampleKey1234567890abcdef", window.ApiKeyTextBox.Text);

            // 3. Toggle reveal (show)
            vm.ToggleKeyVisibilityCommand.Execute(null);
            Assert.True(vm.IsKeyRevealed);
            Assert.Equal("AIzaSyD-sampleKey1234567890abcdef", window.ApiKeyTextBox.Text);
            Assert.Equal("AIzaSyD-sampleKey1234567890abcdef", window.ApiKeyPasswordBox.Password);

            // 4. Modifying unmasked TextBox updates PasswordBox and ViewModel
            window.ApiKeyTextBox.Text = "AIzaSyD-sampleKey1234567890abcdef-updated";
            Assert.Equal("AIzaSyD-sampleKey1234567890abcdef-updated", vm.ApiKeyInput);
            Assert.Equal("AIzaSyD-sampleKey1234567890abcdef-updated", window.ApiKeyPasswordBox.Password);

            // 5. Toggle masking (hide)
            vm.ToggleKeyVisibilityCommand.Execute(null);
            Assert.False(vm.IsKeyRevealed);
            Assert.Equal("AIzaSyD-sampleKey1234567890abcdef-updated", window.ApiKeyPasswordBox.Password);
            Assert.Equal("AIzaSyD-sampleKey1234567890abcdef-updated", window.ApiKeyTextBox.Text);

            window.Close();
        });
    }

    private static void EnsureApplicationResources()
    {
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        var themeUri = new Uri("pack://application:,,,/DOOM.Presentation;component/Themes/DoomTheme.xaml", UriKind.Absolute);
        bool alreadyMerged = app.Resources.MergedDictionaries.Any(d => d.Source == themeUri);
        if (!alreadyMerged)
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
        }
    }

    private static (ResponseOverlayViewModel, AssistantController) CreateOverlayViewModel()
    {
        var textAcquirer = new MockTextAcquisitionService(ValidSelection);
        var aiResponder = new MockAIResponder(["chunk"]);
        var credStore = new MockCredentialStore(ValidApiKey);
        var settingsService = new MockSettings();

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        var privacy = new MockPrivacy();
        var vm = new ResponseOverlayViewModel(controller, privacy);
        return (vm, controller);
    }

    private static SettingsViewModel CreateSettingsViewModel()
    {
        return new SettingsViewModel(
            AppConfiguration.Load(),
            new MockSettings(),
            new MockCredentialStore(ValidApiKey),
            new MockPrivacy(),
            new MockShortcuts(),
            new MockPermissionChecker(),
            new MockAIResponder(["test"]),
            NullLogger<SettingsViewModel>.Instance);
    }

    // --- Mocks ---

    private sealed class MockWindowConfig : IWindowConfigurationService
    {
        public void ApplyOverlayStyles(IntPtr hWnd) {}
        public void ShowNoActivate(IntPtr hWnd) {}
        public void Hide(IntPtr hWnd) {}
        public void PositionTopRight(IntPtr hWnd, int width, int height, int margin = 16) {}
        public bool IsTopMost(IntPtr hWnd) => true;
    }

    private sealed class MockPrivacy : IPrivacyProtectionService
    {
        public PrivacyProtectionState CurrentState { get; set; } = PrivacyProtectionState.Default;
        public event EventHandler<PrivacyProtectionState>? StateChanged { add {} remove {} }
        public bool ApplyPrivacy(IntPtr hWnd, bool enable) => true;
        public PrivacyVerificationStatus VerifyAppliedAffinity(IntPtr hWnd, bool? expectedRequested = null) => PrivacyVerificationStatus.SelfTestPassed;
        public void UpdateSharingContext(SharingContext context) {}
    }

    private sealed class MockSettings : ISettingsService
    {
        public AppSettings CurrentSettings { get; set; } = AppSettings.Default;
        public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentSettings);
        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            CurrentSettings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class MockCredentialStore : ICredentialStore
    {
        private ApiKey? _key;
        public MockCredentialStore(ApiKey? key) => _key = key;
        public Task<bool> HasApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key != null);
        public Task<ApiKey?> ReadApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key);
        public Task SaveApiKeyAsync(ApiKey apiKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteApiKeyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MockShortcuts : IGlobalShortcutService
    {
        public ShortcutStatus Status { get; } = new() { TriggerState = ShortcutRegistrationState.Active };
        public Task RegisterShortcutsAsync(Func<Task> onTrigger, Func<Task> onToggle, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnregisterShortcutsAsync() => Task.CompletedTask;
    }

    private sealed class MockPermissionChecker : IPermissionChecker
    {
        public bool IsRunningAsAdministrator() => false;
        public bool IsUiAutomationAvailable() => true;
    }

    private sealed class MockTextAcquisitionService : ITextAcquisitionService
    {
        private readonly SelectedText _result;
        public MockTextAcquisitionService(SelectedText result) => _result = result;
        public Task<SelectedText> AcquireSelectionAsync(bool allowClipboardFallback, CancellationToken cancellationToken = default) => Task.FromResult(_result);
    }

    private sealed class MockAIResponder : IAIResponding
    {
        private readonly string[] _chunks;
        public MockAIResponder(string[] chunks) => _chunks = chunks;

        public async IAsyncEnumerable<string> StreamResponseAsync(
            GenerationRequest request,
            ApiKey apiKey,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var chunk in _chunks) yield return chunk;
            await Task.CompletedTask;
        }

        public Task<GenerationResult> RespondAsync(GenerationRequest request, ApiKey apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new GenerationResult(string.Join("", _chunks)));
    }
}
