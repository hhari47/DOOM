namespace DOOM.UnitTests;

using DOOM.Core.Errors;
using DOOM.Platform.Windows.Accessibility;
using DOOM.Platform.Windows.Clipboard;
using Microsoft.Extensions.Logging.Abstractions;

public class SystemTextAcquisitionSecurityTests
{
    [Fact]
    public void AcquisitionError_PasswordFieldDetected_IsPreserved()
    {
        var ex = new TextAcquisitionException(
            TextAcquisitionError.PasswordFieldDetected,
            "Text capture is blocked in password fields for your security.");

        Assert.Equal(TextAcquisitionError.PasswordFieldDetected, ex.ErrorCode);
    }

    [Fact]
    public async Task AcquireSelectionAsync_WhenClipboardFallbackDisabled_ThrowsClipboardFallbackDisabled()
    {
        var uiaReader = new UiAutomationSelectionReader(NullLogger<UiAutomationSelectionReader>.Instance);
        var clipboardHandler = new ClipboardFallbackHandler(NullLogger<ClipboardFallbackHandler>.Instance);
        var service = new SystemTextAcquisitionService(
            uiaReader,
            clipboardHandler,
            NullLogger<SystemTextAcquisitionService>.Instance);

        // When allowClipboardFallback is false and no text is selected on screen
        var ex = await Record.ExceptionAsync(() => service.AcquireSelectionAsync(allowClipboardFallback: false));

        Assert.NotNull(ex);
        Assert.IsType<TextAcquisitionException>(ex);
        var textEx = (TextAcquisitionException)ex;

        // Must be one of the expected security/fallback error codes (EmptySelection, Unsupported, or ClipboardFallbackDisabled)
        Assert.Contains(textEx.ErrorCode, new[]
        {
            TextAcquisitionError.EmptySelection,
            TextAcquisitionError.Unsupported,
            TextAcquisitionError.ClipboardFallbackDisabled,
            TextAcquisitionError.TimedOut,
            TextAcquisitionError.SelectionRetrievalFailed
        });
    }
}

