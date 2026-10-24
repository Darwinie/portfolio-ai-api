using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Cloud Run provides the port to listen on through the PORT environment variable and
// routes external traffic to it, so the container must bind to all interfaces.
// Inside a container without PORT (a plain `docker run`) it falls back to 8080, which is
// also Cloud Run's default. Outside a container (dotnet run, Visual Studio) nothing is
// overridden, so launchSettings.json keeps its existing URLs.
var configuredPort = Environment.GetEnvironmentVariable("PORT");
var isRunningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);
var usesContainerEndpoint =
    int.TryParse(configuredPort, out var parsedPort) && parsedPort > 0;

if (usesContainerEndpoint)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{parsedPort}");
}
else if (isRunningInContainer)
{
    builder.WebHost.UseUrls("http://0.0.0.0:8080");
    usesContainerEndpoint = true;
}

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

const string frontendCorsPolicy = "PortfolioFrontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        // Development (Vite dev/preview) plus the deployed frontend origin.
        policy.WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://localhost:4173",
                "http://127.0.0.1:4173",
                "https://lubugandarwin.netlify.app")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Cloud Run terminates TLS at its front end and forwards the caller's address in
// X-Forwarded-For. Rate limits partition on the client IP, so honour that header but
// trust only a single hop: the rightmost value appended by the platform is the real
// client address, and anything the caller prepended is ignored. The proxy addresses are
// dynamic, so no fixed proxy/network allow list can be configured.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Rate limit the public AI endpoints per client IP. Both endpoints call a paid provider.
const string assistantRateLimitPolicy = "portfolio-assistant";
const string triageRateLimitPolicy = "ticket-triage";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(assistantRateLimitPolicy, CreatePerClientPolicy);
    options.AddPolicy(triageRateLimitPolicy, CreatePerClientPolicy);
});

static RateLimitPartition<string> CreatePerClientPolicy(HttpContext context)
{
    var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0
    });
}

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var relativePath = context.Description.RelativePath?.Trim('/');
        var requestExample = relativePath switch
        {
            "api/TicketTriage/analyze" =>
                """
                {
                  "ticketText": "The application crashes on startup and nobody can sign in.",
                  "context": "Production, premium customer",
                  "requestedPriority": "High"
                }
                """,
            "api/TicketTriage/quick" => "\"How do I reset my password?\"",
            "api/PortfolioAssistant/chat" =>
                """
                {
                  "question": "What is Darwin's current role and company?",
                  "history": []
                }
                """,
            _ => null
        };

        if (requestExample is null ||
            operation.RequestBody is not { } requestBody ||
            requestBody.Content is not { } content)
        {
            return Task.CompletedTask;
        }

        foreach (var (contentType, mediaType) in content)
        {
            if (contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                mediaType.Example = JsonNode.Parse(requestExample);
                break;
            }
        }

        return Task.CompletedTask;
    });
});

// Register AI services.
// The per-request timeout is enforced with a linked CancellationTokenSource, so the
// HttpClient timeout is disabled to avoid competing timeout semantics.
builder.Services.Configure<AiProviderOptions>(builder.Configuration.GetSection("AiProviderOptions"));
builder.Services.AddHttpClient(GeminiInteractionClient.HttpClientName, client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddSingleton<IGeminiInteractionClient, GeminiInteractionClient>();
builder.Services.AddSingleton<IAiTriageProvider, GeminiTriageProvider>();
builder.Services.AddSingleton<IPortfolioAssistantProvider, GeminiPortfolioAssistantProvider>();
builder.Services.AddSingleton<JsonPortfolioKnowledgeSource>();
builder.Services.AddSingleton<MarkdownPortfolioKnowledgeSource>();
builder.Services.AddSingleton<IPortfolioKnowledgeSource, CompositePortfolioKnowledgeSource>();
builder.Services.AddScoped<ITicketTriageService>(sp =>
    new TicketTriageService(
        sp.GetRequiredService<IAiTriageProvider>(),
        sp.GetRequiredService<IOptions<AiProviderOptions>>().Value));
builder.Services.AddScoped<IPortfolioAssistantService, PortfolioAssistantService>();

var app = builder.Build();

// Must run before anything that reads the client address or scheme, such as the rate limiter.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Portfolio.AI.Api v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors(frontendCorsPolicy);

// Visual Studio's HTTPS profile listens on both HTTPS and HTTP. The SPA uses the
// HTTP URL in development, and browsers reject CORS preflight requests that are
// redirected, so only enforce HTTPS redirection outside development.
// A container exposes plain HTTP only and TLS is terminated by the hosting platform,
// so redirection is skipped when the container endpoint is used; otherwise it would log
// a warning for every request and could never resolve a redirect target.
if (!app.Environment.IsDevelopment() && !usesContainerEndpoint)
{
    app.UseHttpsRedirection();
}

// Return a safe ProblemDetails response for request bodies rejected by Kestrel
// before model binding. Development exception pages must not expose stack traces.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (BadHttpRequestException exception)
        when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge &&
              context.Request.Path.StartsWithSegments("/api/PortfolioAssistant"))
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Request payload too large.",
            Detail = "The request body must be 32 KB or smaller."
        };
        problem.Extensions["code"] = "request_too_large";

        await context.Response.WriteAsJsonAsync(problem);
    }
});

app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

// Liveness/startup probe endpoint for the container platform. It reports 200 when the
// process is serving requests; it intentionally performs no external calls.
app.MapHealthChecks("/health");

app.Run();
