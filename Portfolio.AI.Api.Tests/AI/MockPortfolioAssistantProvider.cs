using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Models;

namespace Portfolio.AI.Api.Tests.AI;

public sealed class MockPortfolioAssistantProvider : IPortfolioAssistantProvider
{
    public PortfolioAssistantResponse Response { get; set; } = new()
    {
        Answer = "Darwin is a software engineer based in Batangas, Philippines.",
        FoundInKnowledgeBase = true
    };

    public Exception? ExceptionToThrow { get; set; }

    public int CallCount { get; private set; }

    public PortfolioAssistantRequest? LastRequest { get; private set; }

    public string? LastKnowledgeContext { get; private set; }

    public Task<PortfolioAssistantResponse> ChatAsync(
        PortfolioAssistantRequest request,
        string knowledgeContext,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastRequest = request;
        LastKnowledgeContext = knowledgeContext;

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(Response);
    }
}
