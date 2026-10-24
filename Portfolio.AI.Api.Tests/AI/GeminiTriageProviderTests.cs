using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Models;

namespace Portfolio.AI.Api.Tests.AI;

public class GeminiTriageProviderTests
{
    private const string Model = "gemini-3.5-flash-lite";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/";

    [Fact]
    public async Task AnalyzeTicketAsync_WithValidResponse_MapsStructuredFields()
    {
        var handler = new FakeGeminiHandler
        {
            OnPost = (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                InteractionResponse(ValidPayload())))
        };
        var provider = CreateProvider(handler);

        var result = await provider.AnalyzeTicketAsync(ValidRequest());

        Assert.Equal("Bug", result.Category);
        Assert.Equal("We are investigating the crash.", result.SuggestedReply);
        Assert.Equal("High", result.AdjustedPriority);
        Assert.Equal("Production outage affecting sign-in.", result.Explanation);
        Assert.True(result.RequiresUrgentCare);
        Assert.Equal(88, result.ConfidenceScore);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_SendsSystemInstructionAndTicketAsSeparateUntrustedData()
    {
        var handler = new FakeGeminiHandler
        {
            OnPost = (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                InteractionResponse(ValidPayload())))
        };
        var provider = CreateProvider(handler);

        await provider.AnalyzeTicketAsync(ValidRequest());

        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.EndsWith("/v1beta/interactions", post.Path, StringComparison.Ordinal);
        Assert.Equal("test-key", post.ApiKey);

        using var body = JsonDocument.Parse(post.Body!);
        var systemInstruction = body.RootElement.GetProperty("system_instruction").GetString();
        var input = body.RootElement.GetProperty("input").GetString();

        Assert.Equal(Model, body.RootElement.GetProperty("model").GetString());
        Assert.Contains("untrusted", systemInstruction!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("App crashes on startup", systemInstruction);
        Assert.Contains("App crashes on startup", input);
        Assert.Contains("<ticket_data>", input);
        Assert.Equal(1024, body.RootElement
            .GetProperty("generation_config")
            .GetProperty("max_output_tokens")
            .GetInt32());
        Assert.Equal("low", body.RootElement
            .GetProperty("generation_config")
            .GetProperty("thinking_level")
            .GetString());
        Assert.Contains(handler.Requests, r =>
            r.Method == HttpMethod.Get &&
            r.Path.EndsWith($"/v1beta/models/{Model}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithMalformedJson_ThrowsInvalidResponse()
    {
        var handler = new FakeGeminiHandler
        {
            OnPost = (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                InteractionResponse("this is not json")))
        };
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.AnalyzeTicketAsync(ValidRequest()));

        Assert.Equal(AiProviderFailureKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithUnknownCategory_ThrowsInvalidResponse()
    {
        var handler = new FakeGeminiHandler
        {
            OnPost = (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                InteractionResponse(ValidPayload(category: "NotACategory"))))
        };
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.AnalyzeTicketAsync(ValidRequest()));

        Assert.Equal(AiProviderFailureKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithUnknownPriority_ThrowsInvalidResponse()
    {
        var handler = new FakeGeminiHandler
        {
            OnPost = (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                InteractionResponse(ValidPayload(priority: "Critical"))))
        };
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.AnalyzeTicketAsync(ValidRequest()));

        Assert.Equal(AiProviderFailureKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithoutConfidence_ReturnsNullConfidence()
    {
        var handler = new FakeGeminiHandler
        {
            OnPost = (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                InteractionResponse(ValidPayload(includeConfidence: false))))
        };
        var provider = CreateProvider(handler);

        var result = await provider.AnalyzeTicketAsync(ValidRequest());

        Assert.Null(result.ConfidenceScore);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithQuotaExhausted_ThrowsRateLimitedWithoutLeakingProviderMessage()
    {
        const string providerMessage = "quota detail that must not reach clients";
        var handler = new FakeGeminiHandler
        {
            OnPost = (_, _) => Task.FromResult(JsonResponse(
                (HttpStatusCode)429,
                ErrorResponse(429, "RESOURCE_EXHAUSTED", providerMessage)))
        };
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.AnalyzeTicketAsync(ValidRequest()));

        Assert.Equal(AiProviderFailureKind.RateLimited, exception.Kind);
        Assert.DoesNotContain(providerMessage, exception.Message);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithProviderServerError_ThrowsUnavailable()
    {
        var handler = new FakeGeminiHandler
        {
            OnPost = (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.InternalServerError,
                """{"error":{"code":500,"message":"internal detail","status":"INTERNAL"}}"""))
        };
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.AnalyzeTicketAsync(ValidRequest()));

        Assert.Equal(AiProviderFailureKind.Unavailable, exception.Kind);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WhenCallerCancels_ThrowsOperationCanceled()
    {
        var handler = new FakeGeminiHandler
        {
            OnPost = async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable");
            }
        };
        var provider = CreateProvider(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.AnalyzeTicketAsync(ValidRequest(), cancellation.Token));
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WhenRequestTimesOut_ThrowsTimeout()
    {
        var handler = new FakeGeminiHandler
        {
            OnPost = async (_, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                throw new InvalidOperationException("Unreachable");
            }
        };
        var options = CreateOptions();
        options.RequestTimeout = TimeSpan.FromMilliseconds(100);
        var provider = CreateProvider(handler, options);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.AnalyzeTicketAsync(ValidRequest()));

        Assert.Equal(AiProviderFailureKind.Timeout, exception.Kind);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithoutApiKey_ThrowsConfigurationWithoutCallingProvider()
    {
        var handler = new FakeGeminiHandler();
        var options = CreateOptions();
        options.ApiKey = "";
        var provider = CreateProvider(handler, options);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.AnalyzeTicketAsync(ValidRequest()));

        Assert.Equal(AiProviderFailureKind.Configuration, exception.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithOverlongTicket_ThrowsInvalidRequestWithoutCallingProvider()
    {
        var handler = new FakeGeminiHandler();
        var options = CreateOptions();
        options.MaxTicketCharacters = 5;
        var provider = CreateProvider(handler, options);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.AnalyzeTicketAsync(new TicketTriageRequest { TicketText = "123456" }));

        Assert.Equal(AiProviderFailureKind.InvalidRequest, exception.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WhenModelIsNotAvailable_ThrowsConfigurationWithoutPosting()
    {
        var handler = new FakeGeminiHandler
        {
            OnGet = (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.NotFound,
                ErrorResponse(404, "NOT_FOUND", "Model is not found")))
        };
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.AnalyzeTicketAsync(ValidRequest()));

        Assert.Equal(AiProviderFailureKind.Configuration, exception.Kind);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
    }

    private static GeminiTriageProvider CreateProvider(FakeGeminiHandler handler, AiProviderOptions? options = null)
    {
        var providerOptions = Options.Create(options ?? CreateOptions());
        var interactionClient = new GeminiInteractionClient(
            new FakeHttpClientFactory(handler),
            providerOptions,
            NullLogger<GeminiInteractionClient>.Instance);

        return new GeminiTriageProvider(
            interactionClient,
            providerOptions,
            NullLogger<GeminiTriageProvider>.Instance);
    }

    private static AiProviderOptions CreateOptions() => new()
    {
        ApiKey = "test-key",
        ModelName = Model,
        RequestTimeout = TimeSpan.FromSeconds(5),
        MaxTicketCharacters = 8000,
        MaxContextCharacters = 2000,
        MaxOutputTokens = 1024
    };

    private static TicketTriageRequest ValidRequest() => new()
    {
        TicketText = "App crashes on startup and nobody can sign in.",
        Context = "Production, premium customer",
        RequestedPriority = "High"
    };

    private static string ValidPayload(
        string category = "Bug",
        string priority = "High",
        bool includeConfidence = true)
    {
        var payload = new Dictionary<string, object?>
        {
            ["category"] = category,
            ["suggestedReply"] = "We are investigating the crash.",
            ["priority"] = priority,
            ["explanation"] = "Production outage affecting sign-in.",
            ["requiresEscalation"] = true
        };

        if (includeConfidence)
        {
            payload["confidence"] = 88;
        }

        return JsonSerializer.Serialize(payload);
    }

    private static string InteractionResponse(string modelJson) => JsonSerializer.Serialize(new
    {
        id = "test-interaction",
        status = "completed",
        steps = new object[]
        {
            new { type = "thought" },
            new
            {
                type = "model_output",
                content = new[] { new { type = "text", text = modelJson } }
            }
        }
    });

    private static string ErrorResponse(int code, string status, string message) =>
        JsonSerializer.Serialize(new { error = new { code, message, status } });

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed record CapturedRequest(HttpMethod Method, string Path, string? Body, string? ApiKey);

    private sealed class FakeGeminiHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? OnGet { get; init; }

        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? OnPost { get; init; }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var apiKey = request.Headers.TryGetValues("x-goog-api-key", out var values)
                ? values.SingleOrDefault()
                : null;

            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!.AbsolutePath, body, apiKey));

            if (request.Method == HttpMethod.Get)
            {
                return OnGet is not null
                    ? await OnGet(request, cancellationToken)
                    : JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new { name = $"models/{Model}" }));
            }

            if (OnPost is null)
            {
                throw new InvalidOperationException("No POST handler was configured for this test.");
            }

            return await OnPost(request, cancellationToken);
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(BaseUrl)
        };
    }
}
