using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Portfolio.AI.Api.AI;

/// <summary>
/// Shared Gemini transport for AI features. Centralises authentication, model
/// verification, timeout handling, bounded response reads, and safe error mapping.
/// </summary>
public sealed class GeminiInteractionClient : IGeminiInteractionClient
{
    public const string HttpClientName = "Gemini";

    private const int MaxModelTextCharacters = 32768;
    private const int MaxErrorBodyBytes = 16384;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiProviderOptions _options;
    private readonly ILogger<GeminiInteractionClient> _logger;
    private readonly SemaphoreSlim _modelCheckLock = new(1, 1);
    private volatile bool _modelVerified;

    public GeminiInteractionClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AiProviderOptions> options,
        ILogger<GeminiInteractionClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> SendInteractionAsync(
        object requestBody,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestBody);

        var apiKey = _options.ApiKey;
        var modelName = _options.ModelName?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(modelName))
        {
            throw new AiProviderException(
                AiProviderFailureKind.Configuration,
                SafeMessage(AiProviderFailureKind.Configuration));
        }

        ValidateOptions();

        var baseUri = GetBaseUri();
        await EnsureModelAvailableAsync(apiKey, modelName, baseUri, cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.RequestTimeout);

        try
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(baseUri, "v1beta/interactions"));

            httpRequest.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                throw await CreateProviderExceptionAsync(response, "interaction", timeout.Token);
            }

            var responseBody = await ReadBoundedAsync(
                response.Content,
                _options.MaxResponseBytes,
                timeout.Token);

            return ParseInteraction(responseBody);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new AiProviderException(
                AiProviderFailureKind.Timeout,
                SafeMessage(AiProviderFailureKind.Timeout),
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiProviderException(
                AiProviderFailureKind.Unavailable,
                SafeMessage(AiProviderFailureKind.Unavailable),
                exception);
        }
    }

    private void ValidateOptions()
    {
        if (_options.RequestTimeout <= TimeSpan.Zero ||
            _options.MaxOutputTokens <= 0 ||
            _options.MaxResponseBytes <= 0)
        {
            throw new AiProviderException(
                AiProviderFailureKind.Configuration,
                SafeMessage(AiProviderFailureKind.Configuration));
        }
    }

    private Uri GetBaseUri()
    {
        var configured = _options.BaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new AiProviderException(
                AiProviderFailureKind.Configuration,
                SafeMessage(AiProviderFailureKind.Configuration));
        }

        if (!Uri.TryCreate(configured.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new AiProviderException(
                AiProviderFailureKind.Configuration,
                SafeMessage(AiProviderFailureKind.Configuration));
        }

        return baseUri;
    }

    private async Task EnsureModelAvailableAsync(
        string apiKey,
        string modelName,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        if (_modelVerified)
        {
            return;
        }

        await _modelCheckLock.WaitAsync(cancellationToken);
        try
        {
            if (_modelVerified)
            {
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.RequestTimeout);

            try
            {
                using var client = _httpClientFactory.CreateClient(HttpClientName);
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(baseUri, $"v1beta/models/{Uri.EscapeDataString(modelName)}"));

                request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);

                if (!response.IsSuccessStatusCode)
                {
                    throw await CreateProviderExceptionAsync(response, "model lookup", timeout.Token);
                }

                _modelVerified = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                throw new AiProviderException(
                    AiProviderFailureKind.Timeout,
                    SafeMessage(AiProviderFailureKind.Timeout),
                    exception);
            }
            catch (HttpRequestException exception)
            {
                throw new AiProviderException(
                    AiProviderFailureKind.Unavailable,
                    SafeMessage(AiProviderFailureKind.Unavailable),
                    exception);
            }
        }
        finally
        {
            _modelCheckLock.Release();
        }
    }

    private async Task<AiProviderException> CreateProviderExceptionAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        string? googleStatus = null;

        try
        {
            var body = await ReadBoundedAsync(response.Content, MaxErrorBodyBytes, cancellationToken);
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("status", out var status))
            {
                googleStatus = status.GetString();
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Error bodies are diagnostic only. Never rely on them for client-facing messages.
        }

        var kind = MapFailure(response.StatusCode, googleStatus);

        _logger.LogWarning(
            "Gemini {Operation} failed with HTTP {StatusCode} ({GoogleStatus}).",
            operation,
            (int)response.StatusCode,
            googleStatus);

        return new AiProviderException(kind, SafeMessage(kind));
    }

    private static AiProviderFailureKind MapFailure(
        HttpStatusCode statusCode,
        string? googleStatus)
    {
        if (string.Equals(googleStatus, "RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
        {
            return AiProviderFailureKind.RateLimited;
        }

        if (string.Equals(googleStatus, "UNAUTHENTICATED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(googleStatus, "PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(googleStatus, "API_KEY_INVALID", StringComparison.OrdinalIgnoreCase))
        {
            return AiProviderFailureKind.Authentication;
        }

        if (string.Equals(googleStatus, "NOT_FOUND", StringComparison.OrdinalIgnoreCase))
        {
            return AiProviderFailureKind.Configuration;
        }

        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AiProviderFailureKind.Authentication,
            HttpStatusCode.NotFound => AiProviderFailureKind.Configuration,
            HttpStatusCode.TooManyRequests => AiProviderFailureKind.RateLimited,
            >= HttpStatusCode.InternalServerError => AiProviderFailureKind.Unavailable,
            _ => AiProviderFailureKind.Unavailable
        };
    }

    private static string SafeMessage(AiProviderFailureKind kind) => kind switch
    {
        AiProviderFailureKind.Configuration => "The AI provider API key or model is not configured correctly.",
        AiProviderFailureKind.Authentication => "The AI provider rejected the configured credentials.",
        AiProviderFailureKind.RateLimited => "The AI provider quota has been exhausted. Please try again later.",
        AiProviderFailureKind.Timeout => "The AI provider request timed out.",
        AiProviderFailureKind.Unavailable => "The AI provider is currently unavailable.",
        AiProviderFailureKind.InvalidResponse => "The AI provider returned an invalid response.",
        AiProviderFailureKind.InvalidRequest => "The request is not valid.",
        _ => "The AI provider request failed."
    };

    private string ParseInteraction(string responseBody)
    {
        GeminiInteractionResponse? interaction;
        try
        {
            interaction = JsonSerializer.Deserialize<GeminiInteractionResponse>(responseBody, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw InvalidResponse(exception);
        }

        if (interaction is null ||
            !string.Equals(interaction.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidResponse();
        }

        var output = string.Concat(
            (interaction.Steps ?? [])
                .Where(step => string.Equals(step.Type, "model_output", StringComparison.OrdinalIgnoreCase))
                .SelectMany(step => step.Content ?? [])
                .Where(content => string.Equals(content.Type, "text", StringComparison.OrdinalIgnoreCase))
                .Select(content => content.Text ?? string.Empty));

        if (string.IsNullOrWhiteSpace(output) || output.Length > MaxModelTextCharacters)
        {
            throw InvalidResponse();
        }

        return output;
    }

    private AiProviderException InvalidResponse(Exception? innerException = null)
    {
        _logger.LogWarning("Gemini returned an invalid response.");
        return new AiProviderException(
            AiProviderFailureKind.InvalidResponse,
            SafeMessage(AiProviderFailureKind.InvalidResponse),
            innerException);
    }

    private static async Task<string> ReadBoundedAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long contentLength && contentLength > maxBytes)
        {
            throw new AiProviderException(
                AiProviderFailureKind.InvalidResponse,
                SafeMessage(AiProviderFailureKind.InvalidResponse));
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new AiProviderException(
                    AiProviderFailureKind.InvalidResponse,
                    SafeMessage(AiProviderFailureKind.InvalidResponse));
            }

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private sealed class GeminiInteractionResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("steps")]
        public List<GeminiStep>? Steps { get; set; }
    }

    private sealed class GeminiStep
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("content")]
        public List<GeminiContent>? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
