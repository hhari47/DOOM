namespace DOOM.Services.Security;

using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.Errors;
using Microsoft.Extensions.Logging;

/// <summary>
/// Secure credential storage using Windows Data Protection API (DPAPI).
/// Guarantees that API keys are encrypted with user-specific keys and never stored in plaintext on disk.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialStore : ICredentialStore
{
    private readonly string _credentialFilePath;
    private readonly ILogger<DpapiCredentialStore> _logger;
    private readonly object _lock = new();

    // Entropy salt for additional DPAPI defense-in-depth
    private static readonly byte[] Entropy = "DOOM_AI_KEY_PROTECTION_V1"u8.ToArray();

    public DpapiCredentialStore(ILogger<DpapiCredentialStore> logger, string? customFilePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _credentialFilePath = customFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var doomFolder = Path.Combine(appData, "DOOM");
            Directory.CreateDirectory(doomFolder);
            _credentialFilePath = Path.Combine(doomFolder, "credentials.dat");
        }
    }

    public Task<bool> HasApiKeyAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(File.Exists(_credentialFilePath));
        }
    }

    public Task<ApiKey?> ReadApiKeyAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!File.Exists(_credentialFilePath))
            {
                return Task.FromResult<ApiKey?>(null);
            }

            try
            {
                var encryptedBytes = File.ReadAllBytes(_credentialFilePath);
                var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
                var rawKey = Encoding.UTF8.GetString(decryptedBytes);

                if (!ApiKey.TryCreate(rawKey, out var apiKey))
                {
                    _logger.LogWarning("Stored credential exists but fails ApiKey format validation.");
                    throw new CredentialStoreException(CredentialStoreError.InvalidKeyFormat, "Stored credential failed validation.");
                }

                return Task.FromResult<ApiKey?>(apiKey);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "DPAPI decryption failed for credential store.");
                throw new CredentialStoreException(CredentialStoreError.AccessDenied, "Failed to decrypt credentials with DPAPI.", innerException: ex);
            }
            catch (Exception ex) when (ex is not CredentialStoreException)
            {
                _logger.LogError(ex, "Unexpected error reading credential file.");
                throw new CredentialStoreException(CredentialStoreError.Unavailable, "Credential storage is unavailable.", innerException: ex);
            }
        }
    }

    public Task SaveApiKeyAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apiKey);

        lock (_lock)
        {
            var tempPath = _credentialFilePath + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(_credentialFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var rawBytes = Encoding.UTF8.GetBytes(apiKey.AccessKey());
                var encryptedBytes = ProtectedData.Protect(rawBytes, Entropy, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(tempPath, encryptedBytes);
                File.Move(tempPath, _credentialFilePath, overwrite: true);

                _logger.LogInformation("API key stored securely using DPAPI encryption.");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write encrypted credentials to disk.");
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* ignore */ }
                }
                throw new CredentialStoreException(CredentialStoreError.SaveFailed, "Failed to save encrypted credential.", innerException: ex);
            }
        }
    }

    public Task DeleteApiKeyAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_credentialFilePath))
                {
                    File.Delete(_credentialFilePath);
                    _logger.LogInformation("API key credential removed from storage.");
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete credential file.");
                throw new CredentialStoreException(CredentialStoreError.DeleteFailed, "Failed to delete credential.", innerException: ex);
            }
        }
    }
}

