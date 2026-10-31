namespace DOOM.UnitTests;

using System.Net;
using System.Net.Http;
using System.Text;
using DOOM.Core.Domain;
using DOOM.Core.Errors;
using DOOM.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;

public class GeminiServiceTests
{
    private static readonly ApiKey TestKey = ApiKey.Create("AIzaSyD-1234567890abcdefghijklmnopqrstuv");
    private static readonly SourceApplication TestApp = new(10, "TestApp", "Title", "path");
    private static readonly SelectedText SampleText = new("Explain this code", TestApp, AcquisitionMethod.UiAutomation);
    private static readonly GenerationRequest SampleRequest = new(SampleText);

    [Fact]
    public async Task StreamResponseAsync_Success_SendsCorrectHeadersAndYieldsChunks()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var sseContent =
            "data: {\"event_type\":\"step.delta\",\"delta\":{\"type\":\"text\",\"text\":\"Chunk 1 \"}}\n\n" +
            "data: {\"event_type\":\"step.delta\",\"delta\":{\"type\":\"text\",\"text\":\"Chunk 2\"}}\n\n" +
            "data: [DONE]\n\n";

        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedRequest = req;
            if (req.Content != null)
            {
                capturedBody = await req.Content.ReadAsStringAsync();
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream")
            };
            return response;
        });

        using var client = new HttpClient(handler);
        var parser = new GeminiStreamParser(NullLogger<GeminiStreamParser>.Instance);
        var service = new GeminiService(client, parser, NullLogger<GeminiService>.Instance);

        var received = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(SampleRequest, TestKey))
        {
            received.Add(chunk);
        }

        Assert.Equal(2, received.Count);
        Assert.Equal("Chunk 1 ", received[0]);
        Assert.Equal("Chunk 2", received[1]);

        // Verify headers
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest!.Headers.Contains("x-goog-api-key"));
        Assert.Equal(TestKey.AccessKey(), capturedRequest.Headers.GetValues("x-goog-api-key").First());
        Assert.Equal("text/event-stream", capturedRequest.Headers.Accept.First().MediaType);

        // Verify body payload contains store: false
        Assert.NotNull(capturedBody);
        Assert.Contains("\"store\":false", capturedBody);
        Assert.Contains("\"stream\":true", capturedBody);
    }

    [Fact]
    public async Task StreamResponseAsync_Http401_ThrowsInvalidApiKey()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":{\"message\":\"API key not valid\"}}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var client = new HttpClient(handler);
        var parser = new GeminiStreamParser(NullLogger<GeminiStreamParser>.Instance);
        var service = new GeminiService(client, parser, NullLogger<GeminiService>.Instance);

        var ex = await Assert.ThrowsAsync<GenerationException>(async () =>
        {
            await foreach (var _ in service.StreamResponseAsync(SampleRequest, TestKey))
            {
            }
        });

        Assert.Equal(GenerationError.InvalidApiKey, ex.ErrorCode);
        Assert.Equal(401, ex.HttpStatusCode);
    }

    [Fact]
    public async Task StreamResponseAsync_Http429WithRetryAfter_ThrowsRateLimitedWithDuration()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Resource exhausted\"}}", Encoding.UTF8, "application/json")
            };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(25));
            return Task.FromResult(response);
        });

        using var client = new HttpClient(handler);
        var parser = new GeminiStreamParser(NullLogger<GeminiStreamParser>.Instance);
        var service = new GeminiService(client, parser, NullLogger<GeminiService>.Instance);

        var ex = await Assert.ThrowsAsync<GenerationException>(async () =>
        {
            await foreach (var _ in service.StreamResponseAsync(SampleRequest, TestKey))
            {
            }
        });

        Assert.Equal(GenerationError.RateLimited, ex.ErrorCode);
        Assert.Equal(TimeSpan.FromSeconds(25), ex.RetryAfter);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
