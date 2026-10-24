namespace Portfolio.AI.Api.Models;

/// <summary>
/// Response model from Personal AI Portfolio Assistant.
/// </summary>
public class PortfolioAssistantResponse
{
    /// <summary>
    /// Gets or sets the assistant's answer.
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the answer was found in the knowledge base.
    /// </summary>
    public bool FoundInKnowledgeBase { get; set; }

    /// <summary>
    /// Gets or sets a disclaimer message.
    /// </summary>
    public string Disclaimer { get; set; } = "AI-generated answer based on the portfolio knowledge base. May need confirmation.";
}
