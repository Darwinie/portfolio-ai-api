namespace Portfolio.AI.Api.Models;

/// <summary>
/// Response model from AI ticket triage analysis.
/// Contains the resolved category, suggested reply, priority adjustment, and explanation.
/// </summary>
public class TicketTriageResponse
{
    /// <summary>
    /// Gets or sets the predicted ticket category (e.g., Bug, FeatureRequest, Question).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the suggested reply or resolution summary.
    /// </summary>
    public string SuggestedReply { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the adjusted priority level.
    /// </summary>
    public string AdjustedPriority { get; set; } = "Medium";

    /// <summary>
    /// Gets or sets a brief explanation of the classification and priority.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the ticket should be escalated to a human.
    /// </summary>
    public bool RequiresUrgentCare { get; set; }

    /// <summary>
    /// Gets or sets the confidence score of the analysis (0-100).
    /// Null when the model could not meaningfully calibrate its confidence.
    /// </summary>
    public int? ConfidenceScore { get; set; }
}
