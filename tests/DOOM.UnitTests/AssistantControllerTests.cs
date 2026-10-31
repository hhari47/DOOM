namespace DOOM.UnitTests;

using System.Runtime.CompilerServices;
using DOOM.Core.Configuration;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.Errors;
using DOOM.Core.State;
using DOOM.Services.Assistant;
using Microsoft.Extensions.Logging.Abstractions;

public class AssistantControllerTests
{
    private static readonly SourceApplication TestApp = new(10, "TestApp", "Title", "path");
    private static readonly SelectedText ValidSelection = new("Test selected text", TestApp, AcquisitionMethod.UiAutomation);
    private static readonly ApiKey ValidApiKey = ApiKey.Create("AIzaSyD-1234567890abcdefghijklmnopqrstuv");

    [Fact]
    public async Task HandleTriggerAsync_SuccessfulFlow_TransitionsToCompleted()
    {
        var textAcquirer = new MockTextAcquisitionService(ValidSelection);
        var aiResponder = new MockAIResponder(["First chunk, ", "second chunk."]);
        var credStore = new MockCredentialStore(ValidApiKey);
        var settingsService = new MockSettingsService(AppSettings.Default);

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        var stateTransitions = new List<PanelStateType>();
        controller.StateChanged += (_, state) => stateTransitions.Add(state.StateType);

        await controller.HandleTriggerAsync();

        Assert.Equal(PanelStateType.Completed, controller.CurrentState.StateType);
        Assert.Equal("First chunk, second chunk.", controller.CurrentState.Content);
        Assert.Contains(PanelStateType.Acquiring, stateTransitions);
        Assert.Contains(PanelStateType.Loading, stateTransitions);
        Assert.Contains(PanelStateType.Streaming, stateTransitions);
        Assert.Contains(PanelStateType.Completed, stateTransitions);
    }

    [Fact]
    public async Task HandleTriggerAsync_WhenActive_DropsDuplicateTrigger()
    {
        var completionSignal = new TaskCompletionSource<bool>();
        var textAcquirer = new MockTextAcquisitionService(ValidSelection);
        var aiResponder = new DelayedMockAIResponder(completionSignal.Task);
        var credStore = new MockCredentialStore(ValidApiKey);
        var settingsService = new MockSettingsService(AppSettings.Default);

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        // Start first request
        var firstTask = controller.HandleTriggerAsync();

        // While first request is in flight
        Assert.True(controller.IsActive);

        // Fire second request
        await controller.HandleTriggerAsync(); // Should be dropped immediately

        // Complete first request
        completionSignal.SetResult(true);
        await firstTask;

        Assert.False(controller.IsActive);
        Assert.Equal(PanelStateType.Completed, controller.CurrentState.StateType);
    }

    [Fact]
    public async Task CancelCurrentRequest_CancelsActiveRequest()
    {
        var completionSignal = new TaskCompletionSource<bool>();
        var textAcquirer = new MockTextAcquisitionService(ValidSelection);
        var aiResponder = new DelayedMockAIResponder(completionSignal.Task);
        var credStore = new MockCredentialStore(ValidApiKey);
        var settingsService = new MockSettingsService(AppSettings.Default);

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        var requestTask = controller.HandleTriggerAsync();
        Assert.True(controller.IsActive);

        // User triggers Cancel / Escape
        controller.CancelCurrentRequest();

        await requestTask;

        Assert.False(controller.IsActive);
        Assert.Equal(PanelStateType.Cancelled, controller.CurrentState.StateType);
    }

    [Fact]
    public async Task HandleTriggerAsync_MissingApiKey_SetsErrorState()
    {
        var textAcquirer = new MockTextAcquisitionService(ValidSelection);
        var aiResponder = new MockAIResponder(["chunk"]);
        var credStore = new MockCredentialStore(null); // No API key configured
        var settingsService = new MockSettingsService(AppSettings.Default);

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        await controller.HandleTriggerAsync();

        Assert.Equal(PanelStateType.Error, controller.CurrentState.StateType);
        Assert.Equal("MissingApiKey", controller.CurrentState.ErrorCode);
    }

    // --- Mock Helpers ---

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
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var chunk in _chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return chunk;
                await Task.Yield();
            }
        }

        public Task<GenerationResult> RespondAsync(GenerationRequest request, ApiKey apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new GenerationResult(string.Join("", _chunks)));
    }

    private sealed class DelayedMockAIResponder : IAIResponding
    {
        private readonly Task _delayTask;
        public DelayedMockAIResponder(Task delayTask) => _delayTask = delayTask;

        public async IAsyncEnumerable<string> StreamResponseAsync(
            GenerationRequest request,
            ApiKey apiKey,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await _delayTask.WaitAsync(cancellationToken);
            yield return "Finished delayed chunk";
        }

        public Task<GenerationResult> RespondAsync(GenerationRequest request, ApiKey apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new GenerationResult("Finished delayed chunk"));
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
        public AppSettings CurrentSettings { get; }
        public MockSettingsService(AppSettings settings) => CurrentSettings = settings;
        public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentSettings);
        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

