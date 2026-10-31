namespace DOOM.UnitTests;

using System.Runtime.CompilerServices;
using DOOM.Core.Configuration;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.Errors;
using DOOM.Core.State;
using DOOM.Presentation.Markdown;
using DOOM.Services.Assistant;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Phase 7 Reliability and Stress Tests.
/// Validates single-flight concurrency barriers, rapid cancellation, network interruption,
/// consecutive failure cooldown progression, and high-volume SSE streaming.
/// </summary>
public sealed class ReliabilityAndStressTests
{
    private static readonly SourceApplication TestApp = new(500, "StressApp", "Stress Test Window", @"C:\App\app.exe");
    private static readonly SelectedText SampleSelection = new("Stress test payload", TestApp, AcquisitionMethod.UiAutomation);
    private static readonly ApiKey ValidApiKey = ApiKey.Create("AIzaSyD-1234567890abcdefghijklmnopqrstuv");

    [Fact]
    public async Task Stress_TwentyConcurrentHotkeyTriggers_OnlyOneFlightExecutes()
    {
        var completionSignal = new TaskCompletionSource<bool>();
        var textAcquirer = new StubTextAcquirer(SampleSelection);
        var aiResponder = new DelayedAIResponder(completionSignal.Task);
        var credStore = new StubCredStore(ValidApiKey);
        var settingsService = new StubSettings(AppSettings.Default);

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        // 1. Launch 20 concurrent triggers simultaneously
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(controller.HandleTriggerAsync());
        }

        // Give them a moment to compete for the single-flight slot
        await Task.Delay(50);

        Assert.True(controller.IsActive);
        Assert.Equal(1, aiResponder.InvocationCount); // Exactly 1 flight admitted!

        // Release the active stream
        completionSignal.SetResult(true);
        await Task.WhenAll(tasks);

        Assert.False(controller.IsActive);
        Assert.Equal(PanelStateType.Completed, controller.CurrentState.StateType);
    }

    [Fact]
    public async Task Stress_CancelDuringStreaming_ImmediatelyDisposesAndTransitionsToCancelled()
    {
        var tcs = new TaskCompletionSource<bool>();
        var textAcquirer = new StubTextAcquirer(SampleSelection);
        var aiResponder = new StreamingWithDelayAIResponder(tcs.Task);
        var credStore = new StubCredStore(ValidApiKey);
        var settingsService = new StubSettings(AppSettings.Default);

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        var triggerTask = controller.HandleTriggerAsync();

        // Wait until streaming starts
        while (controller.CurrentState.StateType != PanelStateType.Streaming)
        {
            await Task.Delay(10);
        }

        // User presses Escape
        controller.CancelCurrentRequest();

        await triggerTask;

        Assert.Equal(PanelStateType.Cancelled, controller.CurrentState.StateType);
        Assert.False(controller.IsActive);
    }

    [Fact]
    public async Task Stress_NetworkDisconnectionMidStream_TransitionsToErrorState()
    {
        var textAcquirer = new StubTextAcquirer(SampleSelection);
        var aiResponder = new FailingNetworkAIResponder();
        var credStore = new StubCredStore(ValidApiKey);
        var settingsService = new StubSettings(AppSettings.Default);

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        await controller.HandleTriggerAsync();

        Assert.Equal(PanelStateType.Error, controller.CurrentState.StateType);
        Assert.False(controller.IsActive);
    }

    [Fact]
    public async Task Stress_ConsecutiveFailures_EnforcesProgressiveCooldownWindow()
    {
        var textAcquirer = new StubTextAcquirer(SampleSelection);
        var aiResponder = new RateLimitedAIResponder();
        var credStore = new StubCredStore(ValidApiKey);
        var settingsService = new StubSettings(AppSettings.Default);

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        // 1st failure: triggers cooldown
        await controller.HandleTriggerAsync();
        Assert.Equal(PanelStateType.Error, controller.CurrentState.StateType);
        Assert.True(controller.CurrentState.IsRetryable);

        // Immediate subsequent trigger is rejected by Cooldown Policy
        await controller.HandleTriggerAsync();
        Assert.Equal("COOLDOWN_ACTIVE", controller.CurrentState.ErrorCode);
        Assert.Contains("Please wait", controller.CurrentState.ErrorMessage);
    }

    [Fact]
    public void Stress_HighVolumeStreaming_TenThousandCharactersRenderIncrementally()
    {
        // 200 lines of streaming text, totalling ~10,000 chars
        var lines = Enumerable.Range(1, 200)
            .Select(i => $"Line {i}: This is **structured text** with `inline code {i}` and [Ref](https://example.com/ref{i}) in stream.")
            .ToList();

        var accumulated = new System.Text.StringBuilder();
        int updateCount = 0;

        foreach (var line in lines)
        {
            accumulated.AppendLine(line);
            var blocks = IncrementalMarkdownParser.ParseBlocks(accumulated.ToString());
            Assert.NotEmpty(blocks);
            updateCount++;
        }

        Assert.Equal(200, updateCount);
        Assert.True(accumulated.Length > 10000);

        var finalBlocks = IncrementalMarkdownParser.ParseBlocks(accumulated.ToString());
        Assert.True(finalBlocks.Count >= 200);
    }

    // --- Stubs ---

    private sealed class StubTextAcquirer : ITextAcquisitionService
    {
        private readonly SelectedText _selection;
        public StubTextAcquirer(SelectedText selection) => _selection = selection;
        public Task<SelectedText> AcquireSelectionAsync(bool allowClipboardFallback, CancellationToken cancellationToken = default)
            => Task.FromResult(_selection);
    }

    private sealed class DelayedAIResponder : IAIResponding
    {
        private readonly Task _blocker;
        public int InvocationCount { get; private set; }

        public DelayedAIResponder(Task blocker) => _blocker = blocker;

        public async IAsyncEnumerable<string> StreamResponseAsync(
            GenerationRequest request,
            ApiKey apiKey,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            await _blocker.WaitAsync(cancellationToken);
            yield return "Finished";
        }

        public Task<GenerationResult> RespondAsync(GenerationRequest request, ApiKey apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new GenerationResult("Finished"));
    }

    private sealed class StreamingWithDelayAIResponder : IAIResponding
    {
        private readonly Task _pause;
        public StreamingWithDelayAIResponder(Task pause) => _pause = pause;

        public async IAsyncEnumerable<string> StreamResponseAsync(
            GenerationRequest request,
            ApiKey apiKey,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "Chunk 1 ";
            await Task.Yield();
            yield return "Chunk 2 ";

            // Hang until cancellation arrives
            await _pause.WaitAsync(cancellationToken);
            yield return "Should not reach here";
        }

        public Task<GenerationResult> RespondAsync(GenerationRequest request, ApiKey apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new GenerationResult("Done"));
    }

    private sealed class FailingNetworkAIResponder : IAIResponding
    {
        public async IAsyncEnumerable<string> StreamResponseAsync(
            GenerationRequest request,
            ApiKey apiKey,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "Initial chunk received";
            await Task.Yield();
            throw new HttpRequestException("Socket closed abruptly by remote host");
        }

        public Task<GenerationResult> RespondAsync(GenerationRequest request, ApiKey apiKey, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Socket closed abruptly");
    }

    private sealed class RateLimitedAIResponder : IAIResponding
    {
        public async IAsyncEnumerable<string> StreamResponseAsync(
            GenerationRequest request,
            ApiKey apiKey,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new GenerationException(GenerationError.RateLimited, "Gemini quota exceeded. Retry after 2s.");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        public Task<GenerationResult> RespondAsync(GenerationRequest request, ApiKey apiKey, CancellationToken cancellationToken = default)
            => throw new GenerationException(GenerationError.RateLimited, "Gemini quota exceeded. Retry after 2s.");
    }

    private sealed class StubCredStore : ICredentialStore
    {
        private readonly ApiKey? _key;
        public StubCredStore(ApiKey? key) => _key = key;
        public Task<bool> HasApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key != null);
        public Task<ApiKey?> ReadApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key);
        public Task SaveApiKeyAsync(ApiKey apiKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteApiKeyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubSettings : ISettingsService
    {
        public AppSettings CurrentSettings { get; }
        public StubSettings(AppSettings settings) => CurrentSettings = settings;
        public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentSettings);
        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
