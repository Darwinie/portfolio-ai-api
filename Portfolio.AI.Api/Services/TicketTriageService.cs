namespace Portfolio.AI.Api.Services;

/// <summary>
/// Applies service-level policy to the AI provider result.
/// Provider calls, validation, and provider error handling live in the configured <see cref="IAiTriageProvider"/>.
/// </summary>
using Portfolio.AI.Api.Models;
using Portfolio.AI.Api.AI;

public class TicketTriageService : ITicketTriageService
{
    private readonly IAiTriageProvider _aiProvider;
    private readonly AiProviderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketTriageService"/> class.
    /// </summary>
    /// <param name="aiProvider">The AI triage provider for analyzing ticket text.</param>
    /// <param name="options">The AI provider options configuration.</param>
    public TicketTriageService(IAiTriageProvider aiProvider, AiProviderOptions options)
    {
        _aiProvider = aiProvider;
        _options = options;
    }

    /// <summary>
    /// Analyzes a ticket and returns a triage response.
    /// </summary>
    /// <param name="request">The ticket triage request containing text and optional context.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the triage operation. The result contains the category, suggested reply, adjusted priority, and confidence score.</public>
    public async Task<TicketTriageResponse> AnalyzeTicketAsync(TicketTriageRequest request, CancellationToken cancellationToken = default)
    {
        // Delegate to the AI provider for analysis
        var response = await _aiProvider.AnalyzeTicketAsync(request, cancellationToken);

        // Apply options-based adjustments if needed
        if (_options.AdjustPriorityBasedOnContext && !string.IsNullOrEmpty(request.Context))
        {
            // Example: lower priority for tickets with "feature request" context
            if (response.Category.Equals("FeatureRequest", StringComparison.OrdinalIgnoreCase))
            {
                response.AdjustedPriority = "Low";
            }
        }

        return response;
    }
}