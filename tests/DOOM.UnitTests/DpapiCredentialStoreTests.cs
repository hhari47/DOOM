namespace DOOM.UnitTests;

using System.Text;
using DOOM.Core.Domain;
using DOOM.Services.Security;
using Microsoft.Extensions.Logging.Abstractions;

public class DpapiCredentialStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _credentialFilePath;

    public DpapiCredentialStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DOOM_CredTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
        _credentialFilePath = Path.Combine(_tempDirectory, "credentials.dat");
    }

    [Fact]
    public async Task CompleteCredentialLifecycle_SavesEncrypted_ReadsCorrectly_DeletesSafely()
    {
        var store = new DpapiCredentialStore(NullLogger<DpapiCredentialStore>.Instance, _credentialFilePath);

        // 1. Initial State
        Assert.False(await store.HasApiKeyAsync());
        Assert.Null(await store.ReadApiKeyAsync());

        // 2. Save Key
        const string rawKey = "AIzaSyD-1234567890abcdefghijklmnopqrstuv";
        var apiKey = ApiKey.Create(rawKey);
        await store.SaveApiKeyAsync(apiKey);

        Assert.True(await store.HasApiKeyAsync());

        // 3. Verify No Plaintext on Disk
        var fileBytes = await File.ReadAllBytesAsync(_credentialFilePath);
        var fileString = Encoding.UTF8.GetString(fileBytes);
        Assert.DoesNotContain(rawKey, fileString);
        Assert.DoesNotContain("AIza", fileString);

        // 4. Read Decrypted Key
        var retrievedKey = await store.ReadApiKeyAsync();
        Assert.NotNull(retrievedKey);
        Assert.Equal(rawKey, retrievedKey!.AccessKey());

        // 5. Delete Key
        await store.DeleteApiKeyAsync();
        Assert.False(await store.HasApiKeyAsync());
        Assert.Null(await store.ReadApiKeyAsync());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

