namespace DOOM.Services.Assistant;

using System.Text;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.Errors;
using DOOM.Core.State;
using Microsoft.Extensions.Logging;

/// <summary>
/// Single-flight request coordinator managing AI text processing, streaming lifecycle,
/// cancellation, retry cooldowns, and delay notifications.
/// Preserves the original macOS request policy faithfully.
/// </summary>
public sealed class AssistantController : IAsyncDisposable
{
    private static readonly TimeSpan TakingLongerThreshold = TimeSpan.FromSeconds(15);

    private readonly ITextAcquisitionService _textAcquirer;
    private readonly IAIResponding _aiResponder;
    private readonly ICredentialStore _credentialStore;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AssistantController> _logger;

    private readonly object _stateLock = new();
    private readonly RetryCooldownPolicy _cooldownPolicy = new();

    private CancellationTokenSource? _activeCts;
    private Guid _activeFlowId = Guid.Empty;
    private int _consecutiveFailures;
    private PanelState _currentState = PanelState.Idle();

    public PanelState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
    }

    public bool IsActive
    {
        get
        {
            lock (_stateLock)
            {
                return _activeFlowId != Guid.Empty;
            }
        }
    }

    public event EventHandler<PanelState>? StateChanged;
    public event EventHandler<string>? ChunkReceived;

    public AssistantController(
        ITextAcquisitionService textAcquirer,
        IAIResponding aiResponder,
        ICredentialStore credentialStore,
        ISettingsService settingsService,
        ILogger<AssistantController> logger)
    {
        _textAcquirer = textAcquirer ?? throw new ArgumentNullException(nameof(textAcquirer));
        _aiResponder = aiResponder ?? throw new ArgumentNullException(nameof(aiResponder));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initiates the AI assistant workflow from selected text.
    /// Strictly enforces single-flight execution and cooldown windows.
    /// </summary>
    public async Task HandleTriggerAsync()
    {
        Guid flowId;
        CancellationTokenSource cts;

        lock (_stateLock)
        {
            // 1. Single-flight policy: Drop duplicate trigger if a request is already running
            if (_activeFlowId != Guid.Empty)
            {
                _logger.LogInformation("Trigger ignored: single-flight request {FlowId} is already active.", _activeFlowId);
                return;
            }

            // 2. Cooldown check
            if (!_cooldownPolicy.IsAllowed())
            {
                var remaining = _cooldownPolicy.GetRemainingCooldown();
                _logger.LogInformation("Trigger blocked by cooldown policy: {Remaining}s remaining.", remaining.TotalSeconds);
                UpdateState(PanelState.Error($"Please wait {Math.Ceiling(remaining.TotalSeconds)}s before trying again.", "COOLDOWN_ACTIVE", isRetryable: true));
                return;
            }

            flowId = Guid.NewGuid();
            _activeFlowId = flowId;
            _activeCts = new CancellationTokenSource();
            cts = _activeCts;
        }

        _logger.LogInformation("Starting AI request flow {FlowId}...", flowId);

        // 3. Start "Taking longer" watchdog timer (15 seconds)
        using var takingLongerTimer = new Timer(_ =>
        {
            lock (_stateLock)
            {
                if (_activeFlowId == flowId && _currentState.StateType == PanelStateType.Loading)
                {
                    _logger.LogInformation("Flow {FlowId} taking longer than 15s. Notifying UI.", flowId);
                    UpdateState(PanelState.Loading(isTakingLonger: true));
                }
            }
        }, null, TakingLongerThreshold, Timeout.InfiniteTimeSpan);

        try
        {
            // State: Acquiring
            UpdateState(PanelState.Acquiring());

            var settings = _settingsService.CurrentSettings;

            // Step A: Acquire Selected Text
            var selectedText = await _textAcquirer.AcquireSelectionAsync(settings.ClipboardFallbackEnabled, cts.Token);
            _logger.LogInformation("Flow {FlowId}: Text acquired ({Chars} chars) from {Source}.", flowId, selectedText.CharacterCount, selectedText.Source);

            // Step B: Retrieve API Key
            var apiKey = await _credentialStore.ReadApiKeyAsync(cts.Token);
            if (apiKey == null)
            {
                throw new GenerationException(GenerationError.MissingApiKey, "Gemini API key is not configured. Please save your API key in Settings.");
            }

            // State: Loading (Thinking...)
            UpdateState(PanelState.Loading(isTakingLonger: false));

            // Step C: Build Request
            var model = string.IsNullOrWhiteSpace(settings.ModelIdOverride)
                ? ModelId.Default
                : new ModelId(settings.ModelIdOverride);

            var request = new GenerationRequest(selectedText, model: model);

            // Step D: Stream Response
            var fullResponse = new StringBuilder();
            await foreach (var chunk in _aiResponder.StreamResponseAsync(request, apiKey, cts.Token).WithCancellation(cts.Token))
            {
                fullResponse.Append(chunk);

                lock (_stateLock)
                {
                    if (_activeFlowId != flowId)
                    {
                        break;
                    }

                    UpdateState(PanelState.Streaming(fullResponse.ToString()));
                }

                ChunkReceived?.Invoke(this, chunk);
            }

            lock (_stateLock)
            {
                if (_activeFlowId == flowId)
                {
                    _consecutiveFailures = 0;
                    UpdateState(PanelState.Completed(fullResponse.ToString()));
                    _logger.LogInformation("Flow {FlowId} completed successfully ({Length} chars).", flowId, fullResponse.Length);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Flow {FlowId} was cancelled.", flowId);
            lock (_stateLock)
            {
                if (_activeFlowId == flowId)
                {
                    UpdateState(PanelState.Cancelled());
                }
            }
        }
        catch (GenerationException ex)
        {
            _logger.LogError("Flow {FlowId} failed with Gemini generation error: {Error}", flowId, ex.Message);
            HandleFailure(flowId, ex.Message, ex.ErrorCode.ToString(), ex);
        }
        catch (TextAcquisitionException ex)
        {
            _logger.LogWarning("Flow {FlowId} failed during text acquisition: {Error}", flowId, ex.Message);
            HandleFailure(flowId, ex.Message, ex.ErrorCode.ToString(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flow {FlowId} encountered unexpected error.", flowId);
            HandleFailure(flowId, "An unexpected error occurred. Please try again.", "UNEXPECTED_ERROR", null);
        }
        finally
        {
            lock (_stateLock)
            {
                if (_activeFlowId == flowId)
                {
                    _activeFlowId = Guid.Empty;
                    _activeCts?.Dispose();
                    _activeCts = null;
                }
            }
        }
    }

    /// <summary>
    /// Cancels the currently active AI request immediately.
    /// Safe to call even if no request is active.
    /// </summary>
    public void CancelCurrentRequest()
    {
        lock (_stateLock)
        {
            if (_activeFlowId != Guid.Empty && _activeCts != null)
            {
                _logger.LogInformation("Cancelling active AI request flow {FlowId} via Escape/User Action.", _activeFlowId);
                _activeCts.Cancel();
            }
        }
    }

    private void HandleFailure(Guid flowId, string message, string code, GenerationException? genEx)
    {
        lock (_stateLock)
        {
            if (_activeFlowId != flowId)
            {
                return;
            }

            _consecutiveFailures++;

            var isRetryable = false;
            if (genEx != null)
            {
                var cooldown = RetryCooldownPolicy.GetCooldownForError(genEx, _consecutiveFailures);
                if (cooldown.HasValue)
                {
                    isRetryable = true;
                    _cooldownPolicy.RecordAttempt(cooldown.Value);
                    _logger.LogInformation("Recorded cooldown {Cooldown}s after error {Error}.", cooldown.Value.TotalSeconds, code);
                }
            }

            UpdateState(PanelState.Error(message, code, isRetryable));
        }
    }

    private void UpdateState(PanelState newState)
    {
        _currentState = newState;
        StateChanged?.Invoke(this, newState);
    }

    public ValueTask DisposeAsync()
    {
        CancelCurrentRequest();
        return ValueTask.CompletedTask;
    }
}

