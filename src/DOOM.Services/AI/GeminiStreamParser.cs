namespace DOOM.Services.AI;

using System.Runtime.CompilerServices;
using System.Text.Json;
using DOOM.Core.Errors;
using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance Server-Sent Events (SSE) stream parser for Google Gemini API streaming responses.
/// Supports both the modern Interactions API format (step.delta) and classic candidates format.
/// Handles line buffering across split network packets, malformed events, and terminal conditions.
/// </summary>
public sealed class GeminiStreamParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<GeminiStreamParser> _logger;

    public GeminiStreamParser(ILogger<GeminiStreamParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parses an SSE text stream asynchronously, yielding incremental text tokens as they arrive.
    /// </summary>
    public async IAsyncEnumerable<string> ParseStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);

        var yieldedAny = false;
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // SSE comments start with ':'
            if (line.StartsWith(':'))
            {
                continue;
            }

            // Must start with 'data:'
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line.Length > 5 ? line[5..].Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            // Terminal markers
            if (payload.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            string? extractedText = null;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                // 1. Error event in stream
                if (root.TryGetProperty("error", out var errorProp))
                {
                    var msg = errorProp.TryGetProperty("message", out var m) ? m.GetString() : "Streaming error";
                    var code = errorProp.TryGetProperty("code", out var c) ? c.ToString() : "stream_error";
                    _logger.LogError("SSE error event received: {Code} - {Message}", code, msg);
                    throw new GenerationException(GenerationError.Server, msg ?? "Error returned from Gemini stream", providerErrorCode: code);
                }

                // 2. Check for terminal event_type
                if (root.TryGetProperty("event_type", out var eventTypeProp))
                {
                    var eventType = eventTypeProp.GetString();
                    if (string.Equals(eventType, "interaction.completed", StringComparison.OrdinalIgnoreCase))
                    {
                        // Terminal event reached
                        break;
                    }

                    // Modern Interactions format: step.delta
                    if (string.Equals(eventType, "step.delta", StringComparison.OrdinalIgnoreCase) &&
                        root.TryGetProperty("delta", out var deltaProp) &&
                        deltaProp.TryGetProperty("text", out var textProp))
                    {
                        extractedText = textProp.GetString();
                    }
                }

                // 3. Classic format fallback: candidates[0].content.parts[0].text
                if (extractedText == null && root.TryGetProperty("candidates", out var candidatesProp) && candidatesProp.ValueKind == JsonValueKind.Array && candidatesProp.GetArrayLength() > 0)
                {
                    var firstCandidate = candidatesProp[0];
                    if (firstCandidate.TryGetProperty("content", out var contentProp) &&
                        contentProp.TryGetProperty("parts", out var partsProp) &&
                        partsProp.ValueKind == JsonValueKind.Array &&
                        partsProp.GetArrayLength() > 0)
                    {
                        var firstPart = partsProp[0];
                        if (firstPart.TryGetProperty("text", out var partTextProp))
                        {
                            extractedText = partTextProp.GetString();
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Ignoring unparseable SSE line chunk: {Chunk}", payload.Length > 80 ? payload[..80] + "…" : payload);
                continue;
            }

            if (!string.IsNullOrEmpty(extractedText))
            {
                yieldedAny = true;
                yield return extractedText;
            }
        }

        if (!yieldedAny)
        {
            _logger.LogWarning("SSE stream completed without yielding any text content.");
            throw new GenerationException(GenerationError.EmptyResponse, "Gemini returned an empty response.");
        }
    }
}

