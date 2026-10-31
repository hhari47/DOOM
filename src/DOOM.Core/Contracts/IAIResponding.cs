namespace DOOM.Core.Contracts;

using DOOM.Core.Domain;

/// <summary>
/// Service contract for AI response generation and SSE streaming.
/// </summary>
public interface IAIResponding
{
    /// <summary>
    /// Streams Gemini response increments in real time via Server-Sent Events.
    /// </summary>
    IAsyncEnumerable<string> StreamResponseAsync(
        GenerationRequest request,
        ApiKey apiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fallback non-streamed single-response generation.
    /// </summary>
    Task<GenerationResult> RespondAsync(
        GenerationRequest request,
        ApiKey apiKey,
        CancellationToken cancellationToken = default);
}

