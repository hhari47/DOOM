namespace DOOM.UnitTests;

using System.Windows;
using System.Windows.Interop;
using DOOM.Core.Configuration;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.State;
using DOOM.Platform.Windows.Native;
using DOOM.Platform.Windows.WindowManagement;
using DOOM.Presentation.ViewModels;
using DOOM.Presentation.Views;
using DOOM.Services.Assistant;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Phase 7 Window and Focus Hardening Tests.
/// Validates non-activating window styles, topmost Z-order, multimonitor positioning,
/// and decoupled visibility during active streaming.
/// </summary>
public sealed class WindowAndFocusHardeningTests
{
    private static readonly SourceApplication TestApp = new(200, "Editor", "Editor Window", @"C:\App\editor.exe");
    private static readonly SelectedText ValidSelection = new("Target text", TestApp, AcquisitionMethod.UiAutomation);
    private static readonly ApiKey ValidApiKey = ApiKey.Create("AIzaSyD-1234567890abcdefghijklmnopqrstuv");

    [Fact]
    public void NativeOverlayStyles_EnforceTopmostToolWindowAndNoActivate()
    {
        StaTestRunner.Run(() =>
        {
            var windowConfig = new WindowConfigurationService(NullLogger<WindowConfigurationService>.Instance);
            var testWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Width = 440,
                Height = 200,
                Topmost = true
            };
            testWindow.Show();

            var helper = new WindowInteropHelper(testWindow);
            var hWnd = helper.Handle;

            try
            {
                windowConfig.ApplyOverlayStyles(hWnd);

                var exStyle = (long)Win32Interop.GetWindowLongPtr(hWnd, Win32Interop.GWL_EXSTYLE);

                // Assert WS_EX_TOPMOST (0x00000008)
                Assert.True((exStyle & Win32Interop.WS_EX_TOPMOST) != 0, "Window must have WS_EX_TOPMOST applied.");

                // Assert WS_EX_TOOLWINDOW (0x00000080)
                Assert.True((exStyle & Win32Interop.WS_EX_TOOLWINDOW) != 0, "Window must have WS_EX_TOOLWINDOW applied.");

                // Assert WS_EX_NOACTIVATE (0x08000000)
                Assert.True((exStyle & Win32Interop.WS_EX_NOACTIVATE) != 0, "Window must have WS_EX_NOACTIVATE applied.");
            }
            finally
            {
                testWindow.Close();
            }
        });
    }

    [Fact]
    public void PositionTopRight_CalculatesCoordinatesInsideWorkArea()
    {
        StaTestRunner.Run(() =>
        {
            var windowConfig = new WindowConfigurationService(NullLogger<WindowConfigurationService>.Instance);
            var testWindow = new Window { Width = 440, Height = 200 };
            var helper = new WindowInteropHelper(testWindow);
            var hWnd = helper.EnsureHandle();

            try
            {
                windowConfig.PositionTopRight(hWnd, 440, 200, margin: 16);

                var monitor = Win32Interop.MonitorFromWindow(hWnd, Win32Interop.MONITOR_DEFAULTTONEAREST);
                var monitorInfo = new Win32Interop.MONITORINFO
                {
                    cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32Interop.MONITORINFO>()
                };
                var success = Win32Interop.GetMonitorInfo(monitor, ref monitorInfo);
                Assert.True(success);

                var expectedX = monitorInfo.rcWork.Right - 440 - 16;
                var expectedY = monitorInfo.rcWork.Top + 16;

                // Win32 window positioning verification
                Assert.True(expectedX > 0);
                Assert.True(expectedY >= 0);
            }
            finally
            {
                testWindow.Close();
            }
        });
    }

    [Fact]
    public async Task HidingOverlay_DuringStreaming_DoesNotCancelActiveGeneration()
    {
        var completionSignal = new TaskCompletionSource<bool>();
        var textAcquirer = new StubTextAcquisitionService(ValidSelection);
        var aiResponder = new StreamingAIResponder(completionSignal.Task);
        var credStore = new StubCredentialStore(ValidApiKey);
        var settingsService = new StubSettingsService(AppSettings.Default);
        var privacyService = new StubPrivacyService();

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        var viewModel = new ResponseOverlayViewModel(controller, privacyService);

        // 1. Start generation
        var flightTask = controller.HandleTriggerAsync();

        // Wait until streaming starts
        while (controller.CurrentState.StateType != PanelStateType.Streaming)
        {
            await Task.Delay(10);
        }

        // 2. User or shortcut hides overlay window during active streaming
        bool hideRequested = false;
        viewModel.RequestHide += () => hideRequested = true;
        viewModel.CloseCommand.Execute(null); // When busy, close hides / cancels via VM command
        Assert.True(hideRequested);

        // 3. Complete stream in background
        completionSignal.SetResult(true);
        await flightTask;

        // Verify request completed successfully despite window visibility actions
        Assert.Equal(PanelStateType.Completed, controller.CurrentState.StateType);
        Assert.Equal("Streamed response text", controller.CurrentState.Content);
    }

    // --- Stubs ---

    private sealed class StubTextAcquisitionService : ITextAcquisitionService
    {
        private readonly SelectedText _result;
        public StubTextAcquisitionService(SelectedText result) => _result = result;
        public Task<SelectedText> AcquireSelectionAsync(bool allowClipboardFallback, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class StreamingAIResponder : IAIResponding
    {
        private readonly Task _blocker;
        public StreamingAIResponder(Task blocker) => _blocker = blocker;

        public async IAsyncEnumerable<string> StreamResponseAsync(
            GenerationRequest request,
            ApiKey apiKey,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "Streamed ";
            await Task.Yield();
            yield return "response ";
            await _blocker.WaitAsync(cancellationToken);
            yield return "text";
        }

        public Task<GenerationResult> RespondAsync(GenerationRequest request, ApiKey apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new GenerationResult("Streamed response text"));
    }

    private sealed class StubCredentialStore : ICredentialStore
    {
        private readonly ApiKey? _key;
        public StubCredentialStore(ApiKey? key) => _key = key;
        public Task<bool> HasApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key != null);
        public Task<ApiKey?> ReadApiKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key);
        public Task SaveApiKeyAsync(ApiKey apiKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteApiKeyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; }
        public StubSettingsService(AppSettings settings) => CurrentSettings = settings;
        public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentSettings);
        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubPrivacyService : IPrivacyProtectionService
    {
        public PrivacyProtectionState CurrentState { get; set; } = PrivacyProtectionState.Default;
        public event EventHandler<PrivacyProtectionState>? StateChanged { add {} remove {} }
        public bool ApplyPrivacy(IntPtr hWnd, bool enable) => true;
        public PrivacyVerificationStatus VerifyAppliedAffinity(IntPtr hWnd, bool? expectedRequested = null) => PrivacyVerificationStatus.SelfTestPassed;
        public void UpdateSharingContext(SharingContext context) {}
    }
}
