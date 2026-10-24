namespace Portfolio.AI.Api.AI;

/// <summary>
/// Provider-neutral interface for AI ticket triage analysis.
/// Allows switching between Gemini, OpenAI, Anthropic, etc. without changing service code.
/// </summary>
using Portfolio.AI.Api.Models;

public interface IAiTriageProvider
{
    /// <summary>
    /// Analyzes ticket text and returns a triage response.
    /// </summary>
    /// <param name="request">The ticket triage request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the analysis operation. The result contains category, suggested reply, priority, and confidence.</returns>
    Task<TicketTriageResponse> AnalyzeTicketAsync(TicketTriageRequest request, CancellationToken cancellationToken = default);
}