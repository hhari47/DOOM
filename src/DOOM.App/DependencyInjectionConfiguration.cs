namespace DOOM.App;

using DOOM.Core.Configuration;
using DOOM.Core.Contracts;
using DOOM.Platform.Windows.Accessibility;
using DOOM.Platform.Windows.Clipboard;
using DOOM.Platform.Windows.Privacy;
using DOOM.Platform.Windows.Security;
using DOOM.Platform.Windows.Shortcuts;
using DOOM.Platform.Windows.SingleInstance;
using DOOM.Platform.Windows.SystemTray;
using DOOM.Platform.Windows.WindowManagement;
using DOOM.Presentation.ViewModels;
using DOOM.Presentation.Views;
using DOOM.Services.AI;
using DOOM.Services.Assistant;
using DOOM.Services.Security;
using DOOM.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Composition root for DOOM dependency injection.
/// </summary>
public static class DependencyInjectionConfiguration
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services, ISingleInstanceService? singleInstanceService = null)
    {
        // 1. Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

        // 2. Configuration
        var appConfig = AppConfiguration.Load();
        services.AddSingleton(appConfig);

        // 3. Single-Instance Service
        if (singleInstanceService != null)
        {
            services.AddSingleton(singleInstanceService);
        }
        else
        {
            services.AddSingleton<ISingleInstanceService, SingleInstanceService>();
        }

        // 3. Foundation Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICredentialStore, DpapiCredentialStore>();
        services.AddSingleton<IPermissionChecker, WindowsPermissionChecker>();

        // 4. Windows Native Services (Phase 4)
        services.AddSingleton<IWindowConfigurationService, WindowConfigurationService>();
        services.AddSingleton<IPrivacyProtectionService, PrivacyProtectionService>();
        services.AddSingleton<IGlobalShortcutService, GlobalShortcutService>();
        services.AddSingleton<UiAutomationSelectionReader>();
        services.AddSingleton<ClipboardFallbackHandler>();
        services.AddSingleton<ITextAcquisitionService, SystemTextAcquisitionService>();
        services.AddSingleton<ISystemTrayCoordinator, SystemTrayCoordinator>();

        // 5. Gemini AI Services & Assistant (Phase 5)
        services.AddSingleton(GeminiService.CreateDefaultHttpClient());
        services.AddSingleton<GeminiStreamParser>();
        services.AddSingleton<IAIResponding, GeminiService>();
        services.AddSingleton<AssistantController>();

        // 6. Presentation Layer (Phase 6)
        services.AddSingleton(sp => new ResponseOverlayViewModel(
            sp.GetRequiredService<AssistantController>(),
            sp.GetRequiredService<IPrivacyProtectionService>(),
            System.Windows.Application.Current?.Dispatcher));
        services.AddSingleton<ResponseOverlayWindow>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SettingsWindow>();

        // 7. Application Coordinator
        services.AddSingleton<ApplicationCoordinator>();

        return services;
    }
}
