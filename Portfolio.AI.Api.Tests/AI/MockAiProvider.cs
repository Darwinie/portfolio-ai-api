using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Models;
using Portfolio.AI.Api.Services;

namespace Portfolio.AI.Api.Tests.AI;

/// <summary>
/// Mock implementation of IAiTriageProvider for unit testing.
/// Provides canned responses without calling actual AI APIs.
/// </summary>
public class MockAiProvider : IAiTriageProvider
{
    /// <summary>
    /// Gets or sets the response to return from AnalyzeTicketAsync.
    /// </summary>
    public TicketTriageResponse? MockResponse { get; set; }

    /// <summary>
    /// Analyzes a ticket and returns a triage response.
    /// Returns the mock response if set, otherwise a default response.
    /// </summary>
    public Task<TicketTriageResponse> AnalyzeTicketAsync(TicketTriageRequest request, CancellationToken cancellationToken = default)
    {
        if (MockResponse != null)
        {
            return Task.FromResult(MockResponse);
        }

        // Default fallback response
        return Task.FromResult(new TicketTriageResponse
        {
            Category = "General",
            SuggestedReply = "Thank you for your ticket. We'll review and respond shortly.",
            AdjustedPriority = "Medium",
            RequiresUrgentCare = false,
            ConfidenceScore = 50
        });
    }
}