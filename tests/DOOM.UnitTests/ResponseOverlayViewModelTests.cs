namespace DOOM.UnitTests;

using DOOM.Core.Configuration;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.State;
using DOOM.Presentation.ViewModels;
using DOOM.Services.Assistant;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ResponseOverlayViewModelTests
{
    private static readonly SourceApplication TestApp = new(1, "TestApp", "Title", "path");
    private static readonly SelectedText ValidSelection = new("Selected text", TestApp, AcquisitionMethod.UiAutomation);
    private static readonly ApiKey ValidApiKey = ApiKey.Create("AIzaSyD-1234567890abcdefghijklmnopqrstuv");

    [Fact]
    public void InitialState_IsIdleWithDefaultDOOMTitle()
    {
        var (vm, _) = CreateTestViewModel();

        Assert.True(vm.IsIdle);
        Assert.False(vm.IsBusy);
        Assert.False(vm.HasFooter);
        Assert.Equal("DOOM", vm.StatusTitle);
        Assert.Empty(vm.ResponseText);
    }

    [Fact]
    public void ApplyState_Acquiring_TriggersRequestShowAndSetsBusy()
    {
        var (vm, _) = CreateTestViewModel();
        bool showRequested = false;
        vm.RequestShow += () => showRequested = true;

        vm.ApplyState(PanelState.Acquiring());

        Assert.True(vm.IsAcquiring);
        Assert.True(vm.IsBusy);
        Assert.True(vm.HasFooter);
        Assert.True(showRequested);
        Assert.Equal("Reading selection…", vm.StatusTitle);
    }

    [Fact]
    public void ApplyState_Loading_HandlesTakingLongerNotice()
    {
        var (vm, _) = CreateTestViewModel();

        vm.ApplyState(PanelState.Loading(isTakingLonger: true));

        Assert.True(vm.IsLoading);
        Assert.True(vm.IsBusy);
        Assert.True(vm.IsTakingLonger);
        Assert.Equal("Asking Gemini…", vm.StatusTitle);
    }

    [Fact]
    public void ApplyState_Streaming_SetsResponseTextAndRemainsBusy()
    {
        var (vm, _) = CreateTestViewModel();

        vm.ApplyState(PanelState.Streaming("Partial stream content"));

        Assert.True(vm.IsStreaming);
        Assert.True(vm.IsBusy);
        Assert.Equal("Partial stream content", vm.ResponseText);
    }

    [Fact]
    public void ApplyState_Completed_SetsContentAndNotBusy()
    {
        var (vm, _) = CreateTestViewModel();

        vm.ApplyState(PanelState.Completed("Final complete AI response"));

        Assert.True(vm.IsCompleted);
        Assert.False(vm.IsBusy);
        Assert.True(vm.HasFooter);
        Assert.Equal("Final complete AI response", vm.ResponseText);
    }

    [Fact]
    public void ApplyState_Error_SetsErrorPropertiesAndRetryable()
    {
        var (vm, _) = CreateTestViewModel();

        vm.ApplyState(PanelState.Error("Rate limit exceeded", "RATE_LIMIT", isRetryable: true));

        Assert.True(vm.IsError);
        Assert.False(vm.IsBusy);
        Assert.True(vm.IsRetryable);
        Assert.Equal("Rate limit exceeded", vm.ErrorMessage);
        Assert.Equal("RATE_LIMIT", vm.StatusSubtitle);
    }

    [Fact]
    public void CancelCommand_WhenNotBusy_RequestsHide()
    {
        var (vm, _) = CreateTestViewModel();
        bool hideRequested = false;
        vm.RequestHide += () => hideRequested = true;

        vm.CancelCommand.Execute(null);

        Assert.True(hideRequested);
    }

    [Fact]
    public void CloseCommand_RequestsHide()
    {
        var (vm, _) = CreateTestViewModel();
        bool hideRequested = false;
        vm.RequestHide += () => hideRequested = true;

        vm.CloseCommand.Execute(null);

        Assert.True(hideRequested);
    }

    [Fact]
    public void PrivacyStatusLine_ReflectsPrivacyProtectionState()
    {
        var mockPrivacy = new MockPrivacyProtectionService(new PrivacyProtectionState
        {
            IsRequested = true,
            IsApplied = true,
            Verification = PrivacyVerificationStatus.SelfTestPassed
        });

        var (vm, _) = CreateTestViewModel(mockPrivacy);

        Assert.NotNull(vm.PrivacyStatusLine);
        Assert.Contains("Excluded from screen capture (verified)", vm.PrivacyStatusLine);
    }

    private static (ResponseOverlayViewModel ViewModel, AssistantController Controller) CreateTestViewModel(
        IPrivacyProtectionService? privacyService = null)
    {
        var textAcquirer = new MockTextAcquisitionService(ValidSelection);
        var aiResponder = new MockAIResponder(["chunk"]);
        var credStore = new MockCredentialStore(ValidApiKey);
        var settingsService = new MockSettingsService(AppSettings.Default);

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        var privacy = privacyService ?? new MockPrivacyProtectionService(PrivacyProtectionState.Default);

        var vm = new ResponseOverlayViewModel(controller, privacy);
        return (vm, controller);
    }

    // --- Mocks ---

    private sealed class MockPrivacyProtectionService : IPrivacyProtectionService
    {
        public PrivacyProtectionState CurrentState { get; set; }
        public event EventHandler<PrivacyProtectionState>? StateChanged { add {} remove {} }

        public MockPrivacyProtectionService(PrivacyProtectionState state) => CurrentState = state;
        public bool ApplyPrivacy(IntPtr hWnd, bool enable) => true;
        public PrivacyVerificationStatus VerifyAppliedAffinity(IntPtr hWnd, bool? expectedRequested = null) => PrivacyVerificationStatus.SelfTestPassed;
        public void UpdateSharingContext(SharingContext context) {}
    }

    private sealed class MockTextAcquisitionService : ITextAcquisitionService
    {
        private readonly SelectedText _result;
        public MockTextAcquisitionService(SelectedText result) => _result = result;
        public Task<SelectedText> AcquireSelectionAsync(bool allowClipboardFallback, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
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
            foreach (var chunk in _chunks)
            {
                yield return chunk;
            }
            await Task.CompletedTask;
        }

        public Task<GenerationResult> RespondAsync(GenerationRequest request, ApiKey apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new GenerationResult(string.Join("", _chunks)));
    }

    private sealed class MockCredentialStore : ICredentialStore
    {
        private readonly ApiKey? _key;
        public MockCredentialStore(ApiKey? key) => _key = key;
        public Task<bool> HasApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key != null);
        public Task<ApiKey?> ReadApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key);
        public Task SaveApiKeyAsync(ApiKey apiKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteApiKeyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

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
}
