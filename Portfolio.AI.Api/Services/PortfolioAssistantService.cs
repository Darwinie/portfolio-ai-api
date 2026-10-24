using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Models;

namespace Portfolio.AI.Api.Services;

/// <summary>
/// Validates assistant requests and supplies the curated portfolio facts to the AI provider.
/// </summary>
public sealed class PortfolioAssistantService : IPortfolioAssistantService
{
    internal const int MaxQuestionCharacters = 2000;
    internal const int MaxHistoryMessages = 10;
    internal const int MaxHistoryMessageCharacters = 2000;
    internal const int MaxHistoryTotalCharacters = 8000;

    private readonly IPortfolioAssistantProvider _assistantProvider;
    private readonly IPortfolioKnowledgeSource _knowledgeSource;

    public PortfolioAssistantService(
        IPortfolioAssistantProvider assistantProvider,
        IPortfolioKnowledgeSource knowledgeSource)
    {
        _assistantProvider = assistantProvider;
        _knowledgeSource = knowledgeSource;
    }

    public async Task<PortfolioAssistantResponse> ChatAsync(
        PortfolioAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        var knowledgeContext = _knowledgeSource.GetKnowledgeContext();
        return await _assistantProvider.ChatAsync(request, knowledgeContext, cancellationToken);
    }

    private static void ValidateRequest(PortfolioAssistantRequest request)
    {
        var question = request.Question?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new AiProviderException(
                AiProviderFailureKind.InvalidRequest,
                "Question is required.");
        }

        if (question.Length > MaxQuestionCharacters)
        {
            throw new AiProviderException(
                AiProviderFailureKind.InvalidRequest,
                $"Question must be {MaxQuestionCharacters} characters or fewer.");
        }

        if (request.History is null)
        {
            return;
        }

        if (request.History.Count > MaxHistoryMessages)
        {
            throw new AiProviderException(
                AiProviderFailureKind.InvalidRequest,
                $"Conversation history is limited to {MaxHistoryMessages} messages.");
        }

        var totalCharacters = 0;
        foreach (var message in request.History)
        {
            if (message is null)
            {
                throw new AiProviderException(
                    AiProviderFailureKind.InvalidRequest,
                    "History messages cannot be null.");
            }

            if (message.Role is not ("user" or "assistant"))
            {
                throw new AiProviderException(
                    AiProviderFailureKind.InvalidRequest,
                    "History messages must have role 'user' or 'assistant'.");
            }

            var content = message.Content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AiProviderException(
                    AiProviderFailureKind.InvalidRequest,
                    "History message content is required.");
            }

            if (content.Length > MaxHistoryMessageCharacters)
            {
                throw new AiProviderException(
                    AiProviderFailureKind.InvalidRequest,
                    $"Each history message must be {MaxHistoryMessageCharacters} characters or fewer.");
            }

            totalCharacters += content.Length;
        }

        if (totalCharacters > MaxHistoryTotalCharacters)
        {
            throw new AiProviderException(
                AiProviderFailureKind.InvalidRequest,
                $"Conversation history must total {MaxHistoryTotalCharacters} characters or fewer.");
        }
    }
}
