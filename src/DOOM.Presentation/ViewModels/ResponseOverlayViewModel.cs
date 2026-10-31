namespace DOOM.Presentation.ViewModels;

using System.Windows;
using System.Windows.Input;
using DOOM.Core.Contracts;
using DOOM.Core.State;
using DOOM.Presentation.Common;
using DOOM.Services.Assistant;

/// <summary>
/// ViewModel driving the floating response overlay window.
/// Synchronizes with AssistantController, coordinates states, handles cancellation, and copy actions.
/// </summary>
public sealed class ResponseOverlayViewModel : ViewModelBase
{
    private readonly AssistantController _assistantController;
    private readonly IPrivacyProtectionService _privacyService;
    private readonly System.Windows.Threading.Dispatcher? _dispatcher;

    private PanelState _currentState = PanelState.Idle();
    private string _responseText = string.Empty;
    private string _statusTitle = "DOOM";
    private string _statusSubtitle = string.Empty;
    private bool _isTakingLonger;
    private bool _isCopied;
    private string? _errorMessage;
    private bool _isRetryable;
    private int _cooldownSeconds;
    private bool _isVisible;

    public PanelState CurrentState
    {
        get => _currentState;
        private set
        {
            if (SetProperty(ref _currentState, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(IsAcquiring));
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(IsStreaming));
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(IsError));
                OnPropertyChanged(nameof(IsCancelled));
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(HasFooter));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsIdle => CurrentState.StateType == PanelStateType.Idle;
    public bool IsAcquiring => CurrentState.StateType == PanelStateType.Acquiring;
    public bool IsLoading => CurrentState.StateType == PanelStateType.Loading;
    public bool IsStreaming => CurrentState.StateType == PanelStateType.Streaming;
    public bool IsCompleted => CurrentState.StateType == PanelStateType.Completed;
    public bool IsError => CurrentState.StateType == PanelStateType.Error;
    public bool IsCancelled => CurrentState.StateType == PanelStateType.Cancelled;
    public bool IsBusy => IsAcquiring || IsLoading || IsStreaming;
    public bool HasFooter => !IsIdle;

    public string ResponseText
    {
        get => _responseText;
        set => SetProperty(ref _responseText, value);
    }

    public string StatusTitle
    {
        get => _statusTitle;
        set => SetProperty(ref _statusTitle, value);
    }

    public string StatusSubtitle
    {
        get => _statusSubtitle;
        set => SetProperty(ref _statusSubtitle, value);
    }

    public bool IsTakingLonger
    {
        get => _isTakingLonger;
        set => SetProperty(ref _isTakingLonger, value);
    }

    public bool IsCopied
    {
        get => _isCopied;
        set => SetProperty(ref _isCopied, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsRetryable
    {
        get => _isRetryable;
        set => SetProperty(ref _isRetryable, value);
    }

    public int CooldownSeconds
    {
        get => _cooldownSeconds;
        set => SetProperty(ref _cooldownSeconds, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public string? PrivacyStatusLine
    {
        get
        {
            var p = _privacyService.CurrentState;
            if (!p.IsRequested) return null;
            return p.IsProtectionActive
                ? (p.Verification == PrivacyVerificationStatus.SelfTestPassed
                    ? "Excluded from screen capture (verified)"
                    : "Privacy mode active (exclusion applied)")
                : "Privacy mode requested (unverified)";
        }
    }

    // Commands
    public ICommand CancelCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public event Action? RequestHide;
    public event Action? RequestShow;
    public event Action? RequestOpenSettings;

    public ResponseOverlayViewModel(
        AssistantController assistantController,
        IPrivacyProtectionService privacyService,
        System.Windows.Threading.Dispatcher? dispatcher = null)
    {
        _assistantController = assistantController ?? throw new ArgumentNullException(nameof(assistantController));
        _privacyService = privacyService ?? throw new ArgumentNullException(nameof(privacyService));
        _dispatcher = dispatcher;

        CancelCommand = new RelayCommand(ExecuteCancel);
        CloseCommand = new RelayCommand(ExecuteClose);
        CopyCommand = new AsyncRelayCommand(ExecuteCopyAsync);
        RetryCommand = new AsyncRelayCommand(ExecuteRetryAsync);
        OpenSettingsCommand = new RelayCommand(() => RequestOpenSettings?.Invoke());

        // Wire AssistantController events
        _assistantController.StateChanged += OnAssistantStateChanged;
        _assistantController.ChunkReceived += OnAssistantChunkReceived;
        _privacyService.StateChanged += (_, _) => OnPropertyChanged(nameof(PrivacyStatusLine));
    }

    public void ApplyState(PanelState state)
    {
        CurrentState = state;

        switch (state.StateType)
        {
            case PanelStateType.Idle:
                StatusTitle = "DOOM";
                StatusSubtitle = string.Empty;
                ResponseText = string.Empty;
                IsTakingLonger = false;
                ErrorMessage = null;
                break;

            case PanelStateType.Acquiring:
                StatusTitle = "Reading selection…";
                StatusSubtitle = string.Empty;
                ResponseText = string.Empty;
                IsTakingLonger = false;
                ErrorMessage = null;
                RequestShow?.Invoke();
                break;

            case PanelStateType.Loading:
                StatusTitle = "Asking Gemini…";
                StatusSubtitle = string.Empty;
                IsTakingLonger = state.IsTakingLonger;
                ErrorMessage = null;
                RequestShow?.Invoke();
                break;

            case PanelStateType.Streaming:
                StatusTitle = "DOOM";
                StatusSubtitle = string.Empty;
                ResponseText = state.Content ?? string.Empty;
                IsTakingLonger = false;
                ErrorMessage = null;
                RequestShow?.Invoke();
                break;

            case PanelStateType.Completed:
                StatusTitle = "DOOM";
                StatusSubtitle = string.Empty;
                ResponseText = state.Content ?? string.Empty;
                IsTakingLonger = false;
                ErrorMessage = null;
                break;

            case PanelStateType.Error:
                StatusTitle = "Unable to complete";
                StatusSubtitle = state.ErrorCode ?? string.Empty;
                ErrorMessage = state.ErrorMessage ?? "An error occurred.";
                IsRetryable = state.IsRetryable;
                IsTakingLonger = false;
                RequestShow?.Invoke();
                break;

            case PanelStateType.Cancelled:
                StatusTitle = "Request cancelled";
                StatusSubtitle = string.Empty;
                ErrorMessage = null;
                IsTakingLonger = false;
                break;
        }
    }

    private void OnAssistantStateChanged(object? sender, PanelState state)
    {
        if (_dispatcher != null && !_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(() => ApplyState(state));
        }
        else
        {
            ApplyState(state);
        }
    }

    private void OnAssistantChunkReceived(object? sender, string chunk)
    {
        // Chunks are handled via streaming state updates
    }

    private void ExecuteCancel()
    {
        if (IsBusy)
        {
            _assistantController.CancelCurrentRequest();
        }
        else
        {
            RequestHide?.Invoke();
        }
    }

    private void ExecuteClose()
    {
        // Closing or hiding the overlay must NOT cancel an active generation
        RequestHide?.Invoke();
    }

    private async Task ExecuteCopyAsync()
    {
        if (string.IsNullOrEmpty(ResponseText)) return;

        try
        {
            Clipboard.SetText(ResponseText);
            IsCopied = true;
            await Task.Delay(1500);
            IsCopied = false;
        }
        catch
        {
            // Ignore clipboard lock errors gracefully
        }
    }

    private async Task ExecuteRetryAsync()
    {
        if (IsRetryable)
        {
            await _assistantController.HandleTriggerAsync();
        }
    }
}
