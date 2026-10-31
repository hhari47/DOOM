namespace DOOM.UnitTests;

using System.Text;
using DOOM.Core.Errors;
using DOOM.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;

public class GeminiStreamParserTests
{
    private readonly GeminiStreamParser _parser = new(NullLogger<GeminiStreamParser>.Instance);

    [Fact]
    public async Task ParseStreamAsync_StepDeltaFormat_YieldsIncrementalText()
    {
        var sseData =
            "data: {\"event_type\":\"step.delta\",\"delta\":{\"type\":\"text\",\"text\":\"Hello \"},\"index\":0}\n\n" +
            "data: {\"event_type\":\"step.delta\",\"delta\":{\"type\":\"text\",\"text\":\"Windows \"},\"index\":0}\n\n" +
            "data: {\"event_type\":\"step.delta\",\"delta\":{\"type\":\"text\",\"text\":\"DOOM!\"},\"index\":0}\n\n" +
            "data: {\"event_type\":\"interaction.completed\"}\n\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));
        var chunks = new List<string>();

        await foreach (var chunk in _parser.ParseStreamAsync(stream))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(3, chunks.Count);
        Assert.Equal("Hello ", chunks[0]);
        Assert.Equal("Windows ", chunks[1]);
        Assert.Equal("DOOM!", chunks[2]);
    }

    [Fact]
    public async Task ParseStreamAsync_ClassicCandidatesFormat_YieldsIncrementalText()
    {
        var sseData =
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Part 1 \"}]}}]}\n\n" +
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Part 2\"}]}}]}\n\n" +
            "data: [DONE]\n\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));
        var chunks = new List<string>();

        await foreach (var chunk in _parser.ParseStreamAsync(stream))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Part 1 ", chunks[0]);
        Assert.Equal("Part 2", chunks[1]);
    }

    [Fact]
    public async Task ParseStreamAsync_MalformedJsonLine_IgnoresAndContinues()
    {
        var sseData =
            "data: {\"event_type\":\"step.delta\",\"delta\":{\"type\":\"text\",\"text\":\"Valid 1\"}}\n\n" +
            "data: { MALFORMED JSON HERE !!!\n\n" +
            "data: {\"event_type\":\"step.delta\",\"delta\":{\"type\":\"text\",\"text\":\"Valid 2\"}}\n\n" +
            "data: [DONE]\n\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));
        var chunks = new List<string>();

        await foreach (var chunk in _parser.ParseStreamAsync(stream))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Valid 1", chunks[0]);
        Assert.Equal("Valid 2", chunks[1]);
    }

    [Fact]
    public async Task ParseStreamAsync_EmptyStream_ThrowsEmptyResponseException()
    {
        var sseData = ": comment only\n\n\n\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));

        var ex = await Assert.ThrowsAsync<GenerationException>(async () =>
        {
            await foreach (var _ in _parser.ParseStreamAsync(stream))
            {
            }
        });

        Assert.Equal(GenerationError.EmptyResponse, ex.ErrorCode);
    }

    [Fact]
    public async Task ParseStreamAsync_ErrorEvent_ThrowsServerException()
    {
        var sseData = "data: {\"error\":{\"code\":500,\"message\":\"Model overloaded\"}}\n\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));

        var ex = await Assert.ThrowsAsync<GenerationException>(async () =>
        {
            await foreach (var _ in _parser.ParseStreamAsync(stream))
            {
            }
        });

        Assert.Equal(GenerationError.Server, ex.ErrorCode);
        Assert.Contains("Model overloaded", ex.Message);
    }
}

