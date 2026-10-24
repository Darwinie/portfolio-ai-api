using Portfolio.AI.Api.Models;

namespace Portfolio.AI.Api.AI;

public interface IPortfolioAssistantProvider
{
    Task<PortfolioAssistantResponse> ChatAsync(PortfolioAssistantRequest request, string knowledgeContext, CancellationToken cancellationToken = default);
}
