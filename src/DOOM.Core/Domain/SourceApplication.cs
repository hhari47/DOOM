namespace DOOM.Core.Domain;

/// <summary>
/// Identifies the foreground application from which selected text was acquired.
/// </summary>
public sealed record SourceApplication
{
    public static readonly SourceApplication Unknown = new(0, "Unknown", null, null);

    public int ProcessId { get; init; }
    public string ProcessName { get; init; }
    public string? WindowTitle { get; init; }
    public string? ExecutablePath { get; init; }

    public SourceApplication(int processId, string processName, string? windowTitle, string? executablePath)
    {
        ProcessId = processId;
        ProcessName = processName;
        WindowTitle = windowTitle;
        ExecutablePath = executablePath;
    }

    public override string ToString() => string.IsNullOrWhiteSpace(ProcessName) ? "Unknown" : ProcessName;
}

