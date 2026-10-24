namespace Portfolio.AI.Api.Models;

/// <summary>
/// Request model for AI ticket triage analysis.
/// Contains the ticket text and optional context for the AI to analyze.
/// </summary>
public class TicketTriageRequest
{
    /// <summary>
    /// Gets or sets the ticket text content to analyze.
    /// </summary>
    public string TicketText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional additional context (e.g., customer tier, product name).
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Gets or sets the requested priority level if known.
    /// </summary>
    public string? RequestedPriority { get; set; }
}