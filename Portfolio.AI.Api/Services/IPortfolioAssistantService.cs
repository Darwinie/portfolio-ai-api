using Portfolio.AI.Api.Models;

namespace Portfolio.AI.Api.Services;

public interface IPortfolioAssistantService
{
    Task<PortfolioAssistantResponse> ChatAsync(PortfolioAssistantRequest request, CancellationToken cancellationToken = default);
}
