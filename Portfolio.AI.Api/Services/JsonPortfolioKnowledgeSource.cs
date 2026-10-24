using System.Text.Json;
using Portfolio.AI.Api.AI;

namespace Portfolio.AI.Api.Services;

/// <summary>
/// Loads the curated portfolio knowledge from a backend JSON file.
/// The file is intentionally maintained separately from the frontend JavaScript data
/// so the API never depends on frontend source files at runtime.
/// </summary>
public sealed class JsonPortfolioKnowledgeSource : IPortfolioKnowledgeSource
{
    private const string KnowledgeRelativePath = "Data/portfolio-knowledge.json";
    private const string KnowledgeUnavailableMessage = "The portfolio knowledge source is unavailable.";

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<JsonPortfolioKnowledgeSource> _logger;
    private readonly object _loadLock = new();
    private string? _cachedKnowledge;

    public JsonPortfolioKnowledgeSource(
        IWebHostEnvironment environment,
        ILogger<JsonPortfolioKnowledgeSource> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public string GetKnowledgeContext()
    {
        if (_cachedKnowledge is not null)
        {
            return _cachedKnowledge;
        }

        lock (_loadLock)
        {
            if (_cachedKnowledge is not null)
            {
                return _cachedKnowledge;
            }

            var path = Path.Combine(_environment.ContentRootPath, KnowledgeRelativePath);

            try
            {
                if (!File.Exists(path))
                {
                    _logger.LogError("Portfolio knowledge file was not found at {Path}.", path);
                    throw new AiProviderException(
                        AiProviderFailureKind.Configuration,
                        KnowledgeUnavailableMessage);
                }

                var json = File.ReadAllText(path);
                using var document = JsonDocument.Parse(json);

                _cachedKnowledge = json;
                return _cachedKnowledge;
            }
            catch (AiProviderException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError(exception, "Failed to load portfolio knowledge from {Path}.", path);
                throw new AiProviderException(
                    AiProviderFailureKind.Configuration,
                    KnowledgeUnavailableMessage,
                    exception);
            }
        }
    }
}
