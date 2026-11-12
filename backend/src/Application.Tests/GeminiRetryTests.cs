using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TaxNavi.Domain.Models;
using TaxNavi.Infrastructure.Services;
using Xunit;

namespace TaxNavi.Application.Tests;

[Collection("GeminiCooldown")] // the cooldown is process-wide
public class GeminiRetryTests
{
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses;
        public int Calls;

        public ScriptedHandler(params Func<HttpResponseMessage>[] responses) => _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue()() : Ok());
        }
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"Answer [S1]"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":100,"totalTokenCount":140}}""")
    };

    private static HttpResponseMessage Quota(int retrySeconds) => new(HttpStatusCode.TooManyRequests)
    {
        Content = new StringContent("{\"error\":{\"code\":429,\"details\":[{\"@type\":\"type.googleapis.com/google.rpc.RetryInfo\",\"retryDelay\":\"" + retrySeconds + "s\"}]}}")
    };

    private static GeminiLlmService Service(ScriptedHandler handler) =>
        new(new HttpClient(handler), new GeminiOptions { ApiKey = "test", MaxRetries = 4, MaxChatRetryWaitSeconds = 8, QuotaCooldownMinutes = 10 },
            NullLogger<GeminiLlmService>.Instance);

    private static LlmRequest Request() => new() { SystemInstruction = "s", Messages = { new LlmMessage(LlmMessage.UserRole, "q") } };

    [Fact]
    public async Task ShortRateLimit_IsRetried_ThenSucceeds()
    {
        GeminiRetry.ResetCooldown();
        var handler = new ScriptedHandler(() => Quota(0), Ok);

        var result = await Service(handler).CompleteAsync(Request());

        Assert.True(result.Success);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(100, result.InputTokens);
        Assert.Equal(40, result.OutputTokens);
    }

    [Fact]
    public async Task ExhaustedQuota_OpensCooldown_AndLaterCallsSkipGemini()
    {
        GeminiRetry.ResetCooldown();
        var handler = new ScriptedHandler(() => Quota(3600)); // daily quota spent: suggested wait far beyond the chat limit

        var first = await Service(handler).CompleteAsync(Request());
        var second = await Service(handler).CompleteAsync(Request());

        Assert.False(first.Success);
        Assert.Equal("quota", first.Error);
        Assert.True(GeminiRetry.InCooldown);
        Assert.Equal("quota", second.Error);
        Assert.Equal(1, handler.Calls); // the second call never reached Gemini
        GeminiRetry.ResetCooldown();
    }
}
