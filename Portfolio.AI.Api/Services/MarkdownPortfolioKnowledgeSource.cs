using Portfolio.AI.Api.AI;

namespace Portfolio.AI.Api.Services;

/// <summary>
/// Loads the curated, public-safe Markdown profile that supplements the structured
/// portfolio JSON. The private working profile is intentionally not loaded at runtime.
/// </summary>
public sealed class MarkdownPortfolioKnowledgeSource
{
    private const string KnowledgeRelativePath = "Data/portfolio-profile-public.md";
    private const string KnowledgeUnavailableMessage =
        "The supplementary portfolio profile source is unavailable.";

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MarkdownPortfolioKnowledgeSource> _logger;
    private readonly object _loadLock = new();
    private string? _cachedKnowledge;

    public MarkdownPortfolioKnowledgeSource(
        IWebHostEnvironment environment,
        ILogger<MarkdownPortfolioKnowledgeSource> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Gets the public Markdown profile. The result is cached after the first successful load.
    /// </summary>
    /// <exception cref="AiProviderException">
    /// Thrown when the file is missing, empty, invalid, or unreadable.
    /// </exception>
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
                    _logger.LogError("Public portfolio profile file was not found at {Path}.", path);
                    throw new AiProviderException(
                        AiProviderFailureKind.Configuration,
                        KnowledgeUnavailableMessage);
                }

                var markdown = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(markdown) || markdown.Contains('\0'))
                {
                    _logger.LogError(
                        "Public portfolio profile file at {Path} is empty or invalid.",
                        path);
                    throw new AiProviderException(
                        AiProviderFailureKind.Configuration,
                        KnowledgeUnavailableMessage);
                }

                _cachedKnowledge = markdown;
                return _cachedKnowledge;
            }
            catch (AiProviderException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(
                    exception,
                    "Failed to load the public portfolio profile from {Path}.",
                    path);
                throw new AiProviderException(
                    AiProviderFailureKind.Configuration,
                    KnowledgeUnavailableMessage,
                    exception);
            }
        }
    }
}
