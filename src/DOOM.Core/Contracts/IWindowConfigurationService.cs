namespace DOOM.Core.Contracts;

/// <summary>
/// Service contract for configuring native window attributes, Z-order, positioning, and activation behavior.
/// </summary>
public interface IWindowConfigurationService
{
    /// <summary>
    /// Applies floating, non-activating, tool-window styles to an HWND.
    /// </summary>
    void ApplyOverlayStyles(IntPtr hWnd);

    /// <summary>
    /// Shows the window in a non-activating manner (without stealing focus).
    /// </summary>
    void ShowNoActivate(IntPtr hWnd);

    /// <summary>
    /// Hides the window without destroying its native instance or state.
    /// </summary>
    void Hide(IntPtr hWnd);

    /// <summary>
    /// Positions the window at the top-right corner of the active work area with margin.
    /// </summary>
    void PositionTopRight(IntPtr hWnd, int width, int height, int margin = 16);

    /// <summary>
    /// Checks if the window has the topmost style applied.
    /// </summary>
    bool IsTopMost(IntPtr hWnd);
}

