namespace DOOM.UnitTests;

using System.Runtime.CompilerServices;
using DOOM.Core.Configuration;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.Errors;
using DOOM.Core.State;
using DOOM.Presentation.Markdown;
using DOOM.Presentation.ViewModels;
using DOOM.Services.Assistant;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Phase 7 End-to-End Integration Tests.
/// Validates the full workflow: Selection Capture -> Validation -> AssistantController ->
/// Gemini SSE Stream -> ResponseOverlayViewModel -> Markdown Document Rendering.
/// </summary>
public sealed class EndToEndWorkflowIntegrationTests
{
    private static readonly ApiKey ValidApiKey = ApiKey.Create("AIzaSyD-1234567890abcdefghijklmnopqrstuv");
    private static readonly SourceApplication NotepadApp = new(1001, "notepad", "Untitled - Notepad", @"C:\Windows\notepad.exe");
    private static readonly SourceApplication VsCodeApp = new(1002, "Code", "Program.cs - Visual Studio Code", @"C:\Program Files\VS Code\Code.exe");
    private static readonly SourceApplication ChromeApp = new(1003, "chrome", "Google Chrome", @"C:\Program Files\Google\Chrome\chrome.exe");

    [Fact]
    public async Task EndToEnd_NotepadSelection_StreamsAndRendersMarkdownCorrectly()
    {
        // 1. Arrange input selection from Notepad
        var selectedText = "What does this code do?\n```csharp\nvar x = 42;\n```";
        var selection = new SelectedText(selectedText, NotepadApp, AcquisitionMethod.UiAutomation);
        var textAcquirer = new StubTextAcquisitionService(selection);

        var sseChunks = new[]
        {
            "This code declares a variable **x** ",
            "and assigns the integer value `42`.\n\n",
            "- Type: `int`\n",
            "- Value: 42\n\n",
            "See [Documentation](https://learn.microsoft.com) for details."
        };

        var aiResponder = new StubAIResponder(sseChunks);
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
        var capturedStates = new List<PanelStateType>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ResponseOverlayViewModel.CurrentState))
            {
                capturedStates.Add(viewModel.CurrentState.StateType);
            }
        };

        // 2. Act: Trigger full workflow (simulating global hotkey Ctrl+Shift+Space)
        await controller.HandleTriggerAsync();

        // 3. Assert: Workflow lifecycle transitioned properly
        Assert.Contains(PanelStateType.Acquiring, capturedStates);
        Assert.Contains(PanelStateType.Loading, capturedStates);
        Assert.Contains(PanelStateType.Streaming, capturedStates);
        Assert.Contains(PanelStateType.Completed, capturedStates);
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.IsCompleted);

        // 4. Assert: Final response text matches full stream
        var fullExpectedText = string.Join("", sseChunks);
        Assert.Equal(fullExpectedText, viewModel.ResponseText);

        // 5. Assert: Markdown parsing renders valid blocks and adheres to SEC-11 / UI-36
        var blocks = IncrementalMarkdownParser.ParseBlocks(viewModel.ResponseText);
        Assert.NotEmpty(blocks);

        // Verify inline code token
        var allTokens = blocks.SelectMany(b => b.Inlines).ToList();
        Assert.Contains(allTokens, t => t.Type == MarkdownTokenType.Code && t.Text == "42");
        Assert.Contains(allTokens, t => t.Type == MarkdownTokenType.Bold && t.Text == "x");

        // Verify link stripping: Link text preserved, but raw URL stripped
        Assert.Contains(allTokens, t => t.Type == MarkdownTokenType.StrippedLink && t.Text == "Documentation");
        Assert.DoesNotContain(allTokens, t => t.Text.Contains("https://learn.microsoft.com"));
    }

    [Fact]
    public async Task EndToEnd_MalayalamAndUnicodeText_PreservesIntegrityAcrossPipeline()
    {
        // Unicode text test: Malayalam language text
        var malayalamInput = "ഇത് ഒരു പരീക്ഷണമാണ്. ഗൂഗിൾ ജെമിനി എഐ.";
        var selection = new SelectedText(malayalamInput, ChromeApp, AcquisitionMethod.UiAutomation);
        var textAcquirer = new StubTextAcquisitionService(selection);

        var malayalamResponseChunks = new[]
        {
            "ഇത് മലയാളത്തിലുള്ള **വാചകമാണ്**.",
            "\n\nപ്രധാന വിവരങ്ങൾ:",
            "\n- ഗൂഗിൾ ജെമിനി സംഗ്രഹം നൽകുന്നു."
        };

        var aiResponder = new StubAIResponder(malayalamResponseChunks);
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

        await controller.HandleTriggerAsync();

        Assert.Equal(PanelStateType.Completed, viewModel.CurrentState.StateType);
        Assert.Contains("ഇത് മലയാളത്തിലുള്ള **വാചകമാണ്**.", viewModel.ResponseText);

        var blocks = IncrementalMarkdownParser.ParseBlocks(viewModel.ResponseText);
        Assert.Contains(blocks, b => b.Type == MarkdownBlockType.BulletItem);
    }

    [Fact]
    public async Task EndToEnd_PasswordField_StrictlyExcludesFromAcquisitionAndGemini()
    {
        // Secure field detection test: When FocusedElement is a password field
        var textAcquirer = new FailingPasswordTextAcquisitionService();
        var aiResponder = new StubAIResponder(["Should never reach here"]);
        var credStore = new StubCredentialStore(ValidApiKey);
        var settingsService = new StubSettingsService(AppSettings.Default with { ClipboardFallbackEnabled = true });
        var privacyService = new StubPrivacyService();

        var controller = new AssistantController(
            textAcquirer,
            aiResponder,
            credStore,
            settingsService,
            NullLogger<AssistantController>.Instance);

        var viewModel = new ResponseOverlayViewModel(controller, privacyService);

        await controller.HandleTriggerAsync();

        // Must fail with secure field error, never invoking AIResponder
        Assert.Equal(PanelStateType.Error, viewModel.CurrentState.StateType);
        Assert.Equal("PasswordFieldDetected", viewModel.CurrentState.ErrorCode);
        Assert.False(aiResponder.WasInvoked, "AI responder must NEVER be invoked on password fields.");
    }

    [Fact]
    public async Task EndToEnd_NoSelectedText_GracefullyReportsErrorWithoutCallingAI()
    {
        var textAcquirer = new FailingNoSelectionTextAcquisitionService();
        var aiResponder = new StubAIResponder(["Should never reach here"]);
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

        await controller.HandleTriggerAsync();

        Assert.Equal(PanelStateType.Error, viewModel.CurrentState.StateType);
        Assert.Equal("EmptySelection", viewModel.CurrentState.ErrorCode);
        Assert.False(aiResponder.WasInvoked);
    }

    // --- Stubs ---

    private sealed class StubTextAcquisitionService : ITextAcquisitionService
    {
        private readonly SelectedText _result;
        public StubTextAcquisitionService(SelectedText result) => _result = result;
        public Task<SelectedText> AcquireSelectionAsync(bool allowClipboardFallback, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class FailingPasswordTextAcquisitionService : ITextAcquisitionService
    {
        public Task<SelectedText> AcquireSelectionAsync(bool allowClipboardFallback, CancellationToken cancellationToken = default)
            => throw new TextAcquisitionException(TextAcquisitionError.PasswordFieldDetected, "Text capture is blocked in password and secure input fields.");
    }

    private sealed class FailingNoSelectionTextAcquisitionService : ITextAcquisitionService
    {
        public Task<SelectedText> AcquireSelectionAsync(bool allowClipboardFallback, CancellationToken cancellationToken = default)
            => throw new TextAcquisitionException(TextAcquisitionError.EmptySelection, "No text was selected in the active application.");
    }

    private sealed class StubAIResponder : IAIResponding
    {
        private readonly string[] _chunks;
        public bool WasInvoked { get; private set; }

        public StubAIResponder(string[] chunks) => _chunks = chunks;

        public async IAsyncEnumerable<string> StreamResponseAsync(
            GenerationRequest request,
            ApiKey apiKey,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            WasInvoked = true;
            foreach (var chunk in _chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return chunk;
                await Task.Yield();
            }
        }

        public Task<GenerationResult> RespondAsync(GenerationRequest request, ApiKey apiKey, CancellationToken cancellationToken = default)
        {
            WasInvoked = true;
            return Task.FromResult(new GenerationResult(string.Join("", _chunks)));
        }
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
