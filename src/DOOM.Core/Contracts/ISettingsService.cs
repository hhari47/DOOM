namespace DOOM.Core.Contracts;

using DOOM.Core.Configuration;

/// <summary>
/// Contract for persisting and retrieving user preferences.
/// </summary>
public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

