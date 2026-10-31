namespace DOOM.Core.State;

public enum PanelStateType
{
    Idle,
    Acquiring,
    Loading,
    Streaming,
    Completed,
    Error,
    Cancelled
}

public sealed record PanelState
{
    public PanelStateType StateType { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public bool IsRetryable { get; init; }
    public bool IsTakingLonger { get; init; }

    public static PanelState Idle() => new() { StateType = PanelStateType.Idle };
    public static PanelState Acquiring() => new() { StateType = PanelStateType.Acquiring };
    public static PanelState Loading(bool isTakingLonger = false) => new() { StateType = PanelStateType.Loading, IsTakingLonger = isTakingLonger };
    public static PanelState Streaming(string content) => new() { StateType = PanelStateType.Streaming, Content = content };
    public static PanelState Completed(string content) => new() { StateType = PanelStateType.Completed, Content = content };
    public static PanelState Error(string message, string? code = null, bool isRetryable = false) =>
        new() { StateType = PanelStateType.Error, ErrorMessage = message, ErrorCode = code, IsRetryable = isRetryable };
    public static PanelState Cancelled() => new() { StateType = PanelStateType.Cancelled };
}

