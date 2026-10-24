using System.Text;

namespace Portfolio.AI.Api.Services;

/// <summary>
/// Combines the structured JSON facts with the curated public Markdown profile into a
/// single, clearly labelled knowledge context for the assistant.
/// </summary>
public sealed class CompositePortfolioKnowledgeSource : IPortfolioKnowledgeSource
{
    private readonly JsonPortfolioKnowledgeSource _structuredSource;
    private readonly MarkdownPortfolioKnowledgeSource _supplementarySource;
    private readonly object _loadLock = new();
    private string? _cachedKnowledge;

    public CompositePortfolioKnowledgeSource(
        JsonPortfolioKnowledgeSource structuredSource,
        MarkdownPortfolioKnowledgeSource supplementarySource)
    {
        _structuredSource = structuredSource;
        _supplementarySource = supplementarySource;
    }

    /// <summary>
    /// Gets both knowledge sources in labelled sections. The combined result is cached
    /// after the first successful load.
    /// </summary>
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

            var structuredFacts = _structuredSource.GetKnowledgeContext().Trim();
            var supplementaryProfile = _supplementarySource.GetKnowledgeContext().Trim();

            _cachedKnowledge = new StringBuilder()
                .AppendLine("<structured_facts>")
                .AppendLine(structuredFacts)
                .AppendLine("</structured_facts>")
                .AppendLine("<supplementary_profile>")
                .AppendLine(supplementaryProfile)
                .AppendLine("</supplementary_profile>")
                .ToString();

            return _cachedKnowledge;
        }
    }
}
