namespace DOOM.Core.Contracts;

using DOOM.Core.Domain;

/// <summary>
/// Secure credential storage abstraction (Windows Credential Manager / DPAPI).
/// Never stores API keys in plaintext or unprotected files.
/// </summary>
public interface ICredentialStore
{
    Task<bool> HasApiKeyAsync(CancellationToken cancellationToken = default);
    Task<ApiKey?> ReadApiKeyAsync(CancellationToken cancellationToken = default);
    Task SaveApiKeyAsync(ApiKey apiKey, CancellationToken cancellationToken = default);
    Task DeleteApiKeyAsync(CancellationToken cancellationToken = default);
}

