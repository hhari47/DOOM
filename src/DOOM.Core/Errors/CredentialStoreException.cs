namespace DOOM.Core.Errors;

public enum CredentialStoreError
{
    NotFound,
    AccessDenied,
    SaveFailed,
    DeleteFailed,
    InvalidKeyFormat,
    Unavailable
}

public sealed class CredentialStoreException : Exception
{
    public CredentialStoreError ErrorCode { get; }
    public int? NativeErrorCode { get; }

    public CredentialStoreException(CredentialStoreError errorCode, string message, int? nativeErrorCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        NativeErrorCode = nativeErrorCode;
    }
}

public sealed class ConfigurationException : Exception
{
    public string ConfigurationKey { get; }

    public ConfigurationException(string configurationKey, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ConfigurationKey = configurationKey;
    }
}

