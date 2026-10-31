namespace DOOM.Core.Errors;

public enum BlockReason
{
    Safety,
    Recitation,
    ProhibitedContent,
    Spii,
    Blocklist,
    Language,
    Other
}

public enum GenerationError
{
    MissingApiKey,
    CredentialUnavailable,
    Offline,
    TimedOut,
    Transport,
    Cancelled,
    BadRequest,
    UnavailableForAccount,
    InvalidApiKey,
    PaymentRequired,
    PermissionDenied,
    ModelNotFound,
    RateLimited,
    QuotaExceeded,
    Server,
    Blocked,
    EmptyResponse,
    MalformedResponse
}

public sealed class GenerationException : Exception
{
    public GenerationError ErrorCode { get; }
    public int? HttpStatusCode { get; }
    public TimeSpan? RetryAfter { get; }
    public BlockReason? BlockReason { get; }
    public string? ProviderErrorCode { get; }

    public GenerationException(
        GenerationError errorCode,
        string message,
        int? httpStatusCode = null,
        TimeSpan? retryAfter = null,
        BlockReason? blockReason = null,
        string? providerErrorCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        HttpStatusCode = httpStatusCode;
        RetryAfter = retryAfter;
        BlockReason = blockReason;
        ProviderErrorCode = providerErrorCode;
    }
}

