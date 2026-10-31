namespace DOOM.Services.AI;

using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DOOM.Core.Contracts;
using DOOM.Core.Domain;
using DOOM.Core.Errors;
using DOOM.Core.Logging;
using Microsoft.Extensions.Logging;

/// <summary>
/// Production Google Gemini AI service implementing the Interactions API with real SSE streaming.
/// Preserves exact parameters, prompts, token bounds, and security headers from original DOOM.
/// </summary>
public sealed class GeminiService : IAIResponding
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiStreamParser _streamParser;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(
        HttpClient httpClient,
        GeminiStreamParser streamParser,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _streamParser = streamParser ?? throw new ArgumentNullException(nameof(streamParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a production configured HttpClient instance adhering to DOOM security standards:
    /// no cookies, no redirects, 60s timeout, ephemeral handler.
    /// </summary>
    public static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, // SEC-10: Prevent credential leakage via HTTP redirect
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(60),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        GenerationRequest request,
        ApiKey apiKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(apiKey);

        using var httpRequest = BuildHttpRequest(request, apiKey, stream: true);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Gemini request was cancelled by user.");
            throw new GenerationException(GenerationError.Cancelled, "The request was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network transport failure connecting to Gemini API.");
            throw new GenerationException(GenerationError.Offline, "Network connection error.", innerException: ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error dispatching Gemini HTTP request.");
            throw new GenerationException(GenerationError.Transport, "Transport failure.", innerException: ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorResponseAsync(response, cancellationToken);
            }

            var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (responseStream.ConfigureAwait(false))
            {
                await foreach (var chunk in _streamParser.ParseStreamAsync(responseStream, cancellationToken).WithCancellation(cancellationToken))
                {
                    yield return chunk;
                }
            }
        }
    }

    public async Task<GenerationResult> RespondAsync(
        GenerationRequest request,
        ApiKey apiKey,
        CancellationToken cancellationToken = default)
    {
        var fullText = new StringBuilder();
        await foreach (var chunk in StreamResponseAsync(request, apiKey, cancellationToken).WithCancellation(cancellationToken))
        {
            fullText.Append(chunk);
        }

        return new GenerationResult(fullText.ToString());
    }

    private static HttpRequestMessage BuildHttpRequest(GenerationRequest request, ApiKey apiKey, bool stream)
    {
        var payload = new InteractionWirePayload
        {
            Model = request.Model.Value,
            Input = request.Text.Content,
            SystemInstruction = request.SystemInstruction,
            GenerationConfig = new WireGenerationConfig
            {
                ThinkingLevel = request.ThinkingLevel.ToString().ToLowerInvariant(),
                MaxOutputTokens = request.MaxOutputTokens
            },
            Store = false, // Critical privacy requirement
            Stream = stream
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var message = new HttpRequestMessage(HttpMethod.Post, GeminiConfiguration.InteractionsEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Headers
        message.Headers.Accept.Clear();
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(stream ? "text/event-stream" : "application/json"));

        // Key strictly in header (SEC-02)
        message.Headers.Add("x-goog-api-key", apiKey.AccessKey());

        return message;
    }

    private async Task HandleHttpErrorResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        string? rawError = null;
        try
        {
            rawError = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            // Ignore error body read failures
        }

        var sanitizedMessage = SensitiveDataRedactor.SanitizeErrorMessage(rawError);
        _logger.LogError("Gemini API error {StatusCode}: {ErrorBody}", statusCode, sanitizedMessage);

        // Check for Retry-After on 429
        TimeSpan? retryAfter = null;
        if (response.StatusCode == HttpStatusCode.TooManyRequests && response.Headers.RetryAfter != null)
        {
            if (response.Headers.RetryAfter.Delta.HasValue)
            {
                retryAfter = response.Headers.RetryAfter.Delta.Value;
            }
        }

        var error = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => GenerationError.BadRequest,
            HttpStatusCode.Unauthorized => GenerationError.InvalidApiKey,
            HttpStatusCode.PaymentRequired => GenerationError.PaymentRequired,
            HttpStatusCode.Forbidden => GenerationError.PermissionDenied,
            HttpStatusCode.NotFound => GenerationError.ModelNotFound,
            HttpStatusCode.TooManyRequests => GenerationError.RateLimited,
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout => GenerationError.Server,
            _ => GenerationError.Transport
        };

        throw new GenerationException(
            error,
            $"Gemini API failed with status {statusCode}: {sanitizedMessage}",
            httpStatusCode: statusCode,
            retryAfter: retryAfter,
            providerErrorCode: response.StatusCode.ToString());
    }

    private sealed class InteractionWirePayload
    {
        public string Model { get; set; } = string.Empty;
        public string Input { get; set; } = string.Empty;
        public string SystemInstruction { get; set; } = string.Empty;
        public WireGenerationConfig GenerationConfig { get; set; } = new();
        public bool Store { get; set; }
        public bool Stream { get; set; }
    }

    private sealed class WireGenerationConfig
    {
        public string ThinkingLevel { get; set; } = "low";
        public int MaxOutputTokens { get; set; } = 4096;
    }
}

