namespace DOOM.UnitTests;

using DOOM.Core.Configuration;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.State;
using DOOM.Presentation.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class SettingsViewModelTests
{
    private static readonly ApiKey ValidApiKey = ApiKey.Create("AIzaSyD-1234567890abcdefghijklmnopqrstuv");

    [Fact]
    public async Task InitializeAsync_LoadsSettingsAndPopulatesProperties()
    {
        var settings = new AppSettings
        {
            PrivacyEnabled = true,
            ClipboardFallbackEnabled = false,
            ModelIdOverride = "gemini-2.5-flash",
            PrivacyNoticeAcknowledgedVersion = 1
        };

        var (vm, _, _, _) = CreateTestViewModel(settings, ValidApiKey);

        await vm.InitializeAsync();

        Assert.True(vm.PrivacyModeEnabled);
        Assert.False(vm.ClipboardFallbackEnabled);
        Assert.Equal("gemini-2.5-flash", vm.ModelIdInput);
        Assert.True(vm.IsPrivacyReviewed);
        Assert.True(vm.HasApiKey);
        Assert.True(vm.IsSetupComplete);
        Assert.Equal("Ready", vm.StatusText);
    }

    [Fact]
    public async Task SaveApiKeyCommand_ValidKey_EncryptsAndSaves()
    {
        var (vm, _, credStore, _) = CreateTestViewModel(AppSettings.Default, null);
        await vm.InitializeAsync();

        Assert.False(vm.HasApiKey);

        vm.ApiKeyInput = "AIzaSyD-validKey1234567890abcdefghijkl";
        vm.SaveApiKeyCommand.Execute(null);

        // Allow async command execution
        await Task.Delay(50);

        Assert.True(vm.HasApiKey);
        Assert.True(await credStore.HasApiKeyAsync());
        Assert.Null(vm.ApiKeyStatusMessage);
    }

    [Fact]
    public async Task SaveApiKeyCommand_WithSurroundingWhitespace_TrimsAndSavesKey()
    {
        var (vm, _, credStore, _) = CreateTestViewModel(AppSettings.Default, null);
        await vm.InitializeAsync();

        vm.ApiKeyInput = "  AIzaSyD-validKey1234567890abcdefghijkl  \r\n";
        vm.SaveApiKeyCommand.Execute(null);

        await Task.Delay(50);

        Assert.True(vm.HasApiKey);
        var stored = await credStore.ReadApiKeyAsync();
        Assert.NotNull(stored);
        Assert.Equal("AIzaSyD-validKey1234567890abcdefghijkl", stored!.AccessKey());
        Assert.Null(vm.ApiKeyStatusMessage);
    }

    [Fact]
    public async Task SaveApiKeyCommand_WithSpecialCharacters_SavesWithoutRestriction()
    {
        var (vm, _, credStore, _) = CreateTestViewModel(AppSettings.Default, null);
        await vm.InitializeAsync();

        vm.ApiKeyInput = "AIzaSyD.test+key=12345/6789!@#$_-";
        vm.SaveApiKeyCommand.Execute(null);

        await Task.Delay(50);

        Assert.True(vm.HasApiKey);
        var stored = await credStore.ReadApiKeyAsync();
        Assert.NotNull(stored);
        Assert.Equal("AIzaSyD.test+key=12345/6789!@#$_-", stored!.AccessKey());
        Assert.Null(vm.ApiKeyStatusMessage);
    }

    [Fact]
    public async Task SaveApiKeyCommand_BoundaryLengths_20And256_Succeed()
    {
        var (vm, _, credStore, _) = CreateTestViewModel(AppSettings.Default, null);
        await vm.InitializeAsync();

        // 1. Exactly 20 chars
        vm.ApiKeyInput = new string('K', 20);
        vm.SaveApiKeyCommand.Execute(null);
        await Task.Delay(50);
        Assert.True(vm.HasApiKey);

        // 2. Exactly 256 chars (replacing key)
        vm.StartReplaceKeyCommand.Execute(null);
        vm.ApiKeyInput = new string('M', 256);
        vm.SaveApiKeyCommand.Execute(null);
        await Task.Delay(50);
        var stored = await credStore.ReadApiKeyAsync();
        Assert.NotNull(stored);
        Assert.Equal(256, stored!.AccessKey().Length);
    }

    [Fact]
    public async Task SaveApiKeyCommand_TooShort_ShowsUsefulErrorMessage()
    {
        var (vm, _, credStore, _) = CreateTestViewModel(AppSettings.Default, null);
        await vm.InitializeAsync();

        vm.ApiKeyInput = new string('s', 19);
        vm.SaveApiKeyCommand.Execute(null);

        await Task.Delay(50);

        Assert.False(vm.HasApiKey);
        Assert.False(await credStore.HasApiKeyAsync());
        Assert.NotNull(vm.ApiKeyStatusMessage);
        Assert.Contains("too short (19 characters)", vm.ApiKeyStatusMessage);
    }

    [Fact]
    public async Task SaveApiKeyCommand_TooLong_ShowsUsefulErrorMessage()
    {
        var (vm, _, credStore, _) = CreateTestViewModel(AppSettings.Default, null);
        await vm.InitializeAsync();

        vm.ApiKeyInput = new string('L', 257);
        vm.SaveApiKeyCommand.Execute(null);

        await Task.Delay(50);

        Assert.False(vm.HasApiKey);
        Assert.False(await credStore.HasApiKeyAsync());
        Assert.NotNull(vm.ApiKeyStatusMessage);
        Assert.Contains("too long (257 characters)", vm.ApiKeyStatusMessage);
    }

    [Fact]
    public async Task ApiKeyInput_Modification_ClearsStaleStatusMessage()
    {
        var (vm, _, _, _) = CreateTestViewModel(AppSettings.Default, null);
        await vm.InitializeAsync();

        vm.ApiKeyStatusMessage = "Stale error message";
        Assert.NotNull(vm.ApiKeyStatusMessage);

        vm.ApiKeyInput = "new input";
        Assert.Null(vm.ApiKeyStatusMessage);
    }

    [Fact]
    public async Task SaveApiKeyCommand_InvalidFormat_RejectsAndSetsMessage()
    {
        var (vm, _, credStore, _) = CreateTestViewModel(AppSettings.Default, null);
        await vm.InitializeAsync();

        vm.ApiKeyInput = "short"; // Less than 20 chars
        vm.SaveApiKeyCommand.Execute(null);

        await Task.Delay(50);

        Assert.False(vm.HasApiKey);
        Assert.False(await credStore.HasApiKeyAsync());
        Assert.NotNull(vm.ApiKeyStatusMessage);
    }

    [Fact]
    public async Task RemoveApiKeyCommand_DeletesKeyFromStore()
    {
        var (vm, _, credStore, _) = CreateTestViewModel(AppSettings.Default, ValidApiKey);
        await vm.InitializeAsync();

        Assert.True(vm.HasApiKey);

        vm.RemoveApiKeyCommand.Execute(null);
        await Task.Delay(50);

        Assert.False(vm.HasApiKey);
        Assert.False(await credStore.HasApiKeyAsync());
    }

    [Fact]
    public async Task SaveModelCommand_And_ResetModelCommand_WorkCorrectly()
    {
        var (vm, settingsService, _, _) = CreateTestViewModel(AppSettings.Default, ValidApiKey);
        await vm.InitializeAsync();

        // 1. Override model
        vm.ModelIdInput = "gemini-2.5-flash";
        Assert.True(vm.CanResetModel);

        vm.SaveModelCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal("gemini-2.5-flash", settingsService.CurrentSettings.ModelIdOverride);

        // 2. Reset model
        vm.ResetModelCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal(ModelId.Default.Value, vm.ModelIdInput);
        Assert.False(vm.CanResetModel);
        Assert.Null(settingsService.CurrentSettings.ModelIdOverride);
    }

    [Fact]
    public async Task PrivacyModeEnabled_Toggle_UpdatesSettings()
    {
        var (vm, settingsService, _, _) = CreateTestViewModel(AppSettings.Default with { PrivacyEnabled = false }, ValidApiKey);
        await vm.InitializeAsync();

        Assert.False(vm.PrivacyModeEnabled);

        vm.PrivacyModeEnabled = true;
        await Task.Delay(50);

        Assert.True(settingsService.CurrentSettings.PrivacyEnabled);
    }

    [Fact]
    public async Task ClipboardFallbackEnabled_Toggle_UpdatesSettings()
    {
        var (vm, settingsService, _, _) = CreateTestViewModel(AppSettings.Default with { ClipboardFallbackEnabled = false }, ValidApiKey);
        await vm.InitializeAsync();

        Assert.False(vm.ClipboardFallbackEnabled);

        vm.ClipboardFallbackEnabled = true;
        await Task.Delay(50);

        Assert.True(settingsService.CurrentSettings.ClipboardFallbackEnabled);
    }

    [Fact]
    public async Task KeyVisibility_Toggle_TogglesIsKeyRevealedAndShowHideText()
    {
        var (vm, _, _, _) = CreateTestViewModel(AppSettings.Default, null);
        await vm.InitializeAsync();

        Assert.False(vm.IsKeyRevealed);
        Assert.Equal("Show", vm.ShowHideButtonText);

        vm.ToggleKeyVisibilityCommand.Execute(null);

        Assert.True(vm.IsKeyRevealed);
        Assert.Equal("Hide", vm.ShowHideButtonText);

        vm.ToggleKeyVisibilityCommand.Execute(null);

        Assert.False(vm.IsKeyRevealed);
        Assert.Equal("Show", vm.ShowHideButtonText);
    }

    [Fact]
    public async Task CanShowInputField_ReflectsKeyPresenceAndReplacingState()
    {
        // 1. When no key is configured, input field is shown
        var (vm, _, credStore, _) = CreateTestViewModel(AppSettings.Default, null);
        await vm.InitializeAsync();

        Assert.False(vm.HasApiKey);
        Assert.True(vm.CanShowInputField);
        Assert.Equal("Not configured", vm.ApiKeyStatusText);
        Assert.Equal("Required", vm.ApiKeyBadgeText);

        // 2. When key is saved, input field is hidden
        vm.ApiKeyInput = "AIzaSyD-validKey1234567890abcdefghijkl";
        vm.SaveApiKeyCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(vm.HasApiKey);
        Assert.False(vm.IsReplacingKey);
        Assert.False(vm.CanShowInputField);
        Assert.Equal("Saved securely via Windows DPAPI", vm.ApiKeyStatusText);
        Assert.Equal("Configured", vm.ApiKeyBadgeText);

        // 3. When replacing key is requested, input field is shown
        vm.StartReplaceKeyCommand.Execute(null);
        Assert.True(vm.IsReplacingKey);
        Assert.True(vm.CanShowInputField);

        // 4. When cancelling replacement, input field is hidden again
        vm.CancelReplaceKeyCommand.Execute(null);
        Assert.False(vm.IsReplacingKey);
        Assert.False(vm.CanShowInputField);
    }

    [Fact]
    public async Task TestConnectionCommand_WithSavedKey_ReportsSuccess()
    {
        var (vm, _, _, _) = CreateTestViewModel(AppSettings.Default, ValidApiKey);
        await vm.InitializeAsync();

        Assert.True(vm.HasApiKey);
        Assert.Null(vm.TestConnectionResult);

        vm.TestConnectionCommand.Execute(null);
        await Task.Delay(100);

        Assert.NotNull(vm.TestConnectionResult);
        Assert.True(vm.IsTestConnectionSuccess);
        Assert.Contains("success", vm.TestConnectionResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcknowledgePrivacyNoticeCommand_UpdatesSettingsVersion()
    {
        var (vm, settingsService, _, _) = CreateTestViewModel(AppSettings.Default with { PrivacyNoticeAcknowledgedVersion = 0 }, ValidApiKey);
        await vm.InitializeAsync();

        Assert.False(vm.IsPrivacyReviewed);

        vm.AcknowledgePrivacyNoticeCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(vm.IsPrivacyReviewed);
        Assert.Equal(1, settingsService.CurrentSettings.PrivacyNoticeAcknowledgedVersion);
    }

    private static (SettingsViewModel ViewModel, MockSettingsService SettingsService, MockCredentialStore CredStore, MockPrivacyProtectionService PrivacyService)
        CreateTestViewModel(AppSettings settings, ApiKey? initialKey)
    {
        var config = AppConfiguration.Load();
        var settingsService = new MockSettingsService(settings);
        var credStore = new MockCredentialStore(initialKey);
        var privacyService = new MockPrivacyProtectionService(PrivacyProtectionState.Default);
        var shortcutService = new MockGlobalShortcutService();
        var permissionChecker = new MockPermissionChecker();
        var aiResponder = new MockAIResponder(["test response"]);

        var vm = new SettingsViewModel(
            config,
            settingsService,
            credStore,
            privacyService,
            shortcutService,
            permissionChecker,
            aiResponder,
            NullLogger<SettingsViewModel>.Instance);

        return (vm, settingsService, credStore, privacyService);
    }

    // --- Mock Helpers ---

    private sealed class MockSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; set; }
        public MockSettingsService(AppSettings settings) => CurrentSettings = settings;
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
        public Task SaveApiKeyAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
        {
            _key = apiKey;
            return Task.CompletedTask;
        }
        public Task DeleteApiKeyAsync(CancellationToken cancellationToken = default)
        {
            _key = null;
            return Task.CompletedTask;
        }
    }

    private sealed class MockPrivacyProtectionService : IPrivacyProtectionService
    {
        public PrivacyProtectionState CurrentState { get; set; }
        public event EventHandler<PrivacyProtectionState>? StateChanged { add {} remove {} }

        public MockPrivacyProtectionService(PrivacyProtectionState state) => CurrentState = state;
        public bool ApplyPrivacy(IntPtr hWnd, bool enable) => true;
        public PrivacyVerificationStatus VerifyAppliedAffinity(IntPtr hWnd, bool? expectedRequested = null) => PrivacyVerificationStatus.SelfTestPassed;
        public void UpdateSharingContext(SharingContext context) {}
    }

    private sealed class MockGlobalShortcutService : IGlobalShortcutService
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
