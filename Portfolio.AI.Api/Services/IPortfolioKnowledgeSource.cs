namespace Portfolio.AI.Api.Services;

/// <summary>
/// Provides the curated portfolio knowledge used by the assistant.
/// </summary>
public interface IPortfolioKnowledgeSource
{
    /// <summary>
    /// Gets the portfolio knowledge context, including labelled structured facts and
    /// supplementary public profile sections. The result is cached after the first
    /// successful load.
    /// </summary>
    string GetKnowledgeContext();
}
