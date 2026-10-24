namespace Portfolio.AI.Api.Services;

/// <summary>
/// Interface for ticket triage service that analyzes ticket text using AI.
/// Decouples the controller from the AI implementation.
/// </summary>
using Portfolio.AI.Api.Models;

public interface ITicketTriageService
{
    /// <summary>
    /// Analyzes a ticket and returns a triage response with category, suggested reply, and priority.
    /// </summary>
    /// <param name="request">The ticket triage request containing text and optional context.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the triage operation. The result contains the category, suggested reply, adjusted priority, and confidence score.</returns>
    Task<TicketTriageResponse> AnalyzeTicketAsync(TicketTriageRequest request, CancellationToken cancellationToken = default);
}