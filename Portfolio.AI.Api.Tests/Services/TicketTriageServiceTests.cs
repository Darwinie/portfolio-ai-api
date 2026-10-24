using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Models;
using Portfolio.AI.Api.Services;
using Portfolio.AI.Api.Tests.AI;

namespace Portfolio.AI.Api.Tests.Services;

/// <summary>
/// Unit tests for the TicketTriageService class.
/// Uses mocked AI provider to test service logic without API costs.
/// </summary>
public class TicketTriageServiceTests
{
    private readonly MockAiProvider _mockProvider;
    private readonly TicketTriageService _service;

    public TicketTriageServiceTests()
    {
        _mockProvider = new MockAiProvider();
        _service = new TicketTriageService(_mockProvider, new AiProviderOptions());
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        var request = new TicketTriageRequest
        {
            TicketText = "Bug: Application crashes when clicking submit button",
            Context = "Customer is on Premium tier",
            RequestedPriority = "High"
        };

        _mockProvider.MockResponse = new TicketTriageResponse
        {
            Category = "Bug",
            SuggestedReply = "We'll investigate the crash issue.",
            AdjustedPriority = "High",
            RequiresUrgentCare = true,
            ConfidenceScore = 90
        };

        // Act
        var result = await _service.AnalyzeTicketAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Bug", result.Category);
        Assert.Equal("We'll investigate the crash issue.", result.SuggestedReply);
        Assert.Equal("High", result.AdjustedPriority);
        Assert.True(result.RequiresUrgentCare);
        Assert.Equal(90, result.ConfidenceScore);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithFeatureRequest_ReturnsFeatureCategory()
    {
        // Arrange
        var request = new TicketTriageRequest
        {
            TicketText = "Feature request: Add dark mode support to the application",
            Context = null,
            RequestedPriority = "Medium"
        };

        _mockProvider.MockResponse = new TicketTriageResponse
        {
            Category = "FeatureRequest",
            SuggestedReply = "Thank you for the feature request. We'll consider it for our roadmap.",
            AdjustedPriority = "Low",
            RequiresUrgentCare = false,
            ConfidenceScore = 80
        };

        // Act
        var result = await _service.AnalyzeTicketAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FeatureRequest", result.Category);
        Assert.Equal("Thank you for the feature request. We'll consider it for our roadmap.", result.SuggestedReply);
        Assert.Equal("Low", result.AdjustedPriority);  // Service should lower priority for features
        Assert.False(result.RequiresUrgentCare);
        Assert.Equal(80, result.ConfidenceScore);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithQuestion_ReturnsQuestionCategory()
    {
        // Arrange
        var request = new TicketTriageRequest
        {
            TicketText = "How do I reset my password?",
            Context = null,
            RequestedPriority = "Medium"
        };

        _mockProvider.MockResponse = new TicketTriageResponse
        {
            Category = "Question",
            SuggestedReply = "Please use the 'Forgot Password' link on the login page.",
            AdjustedPriority = "Medium",
            RequiresUrgentCare = false,
            ConfidenceScore = 85
        };

        // Act
        var result = await _service.AnalyzeTicketAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Question", result.Category);
        Assert.Equal("Please use the 'Forgot Password' link on the login page.", result.SuggestedReply);
        Assert.Equal("Medium", result.AdjustedPriority);
        Assert.False(result.RequiresUrgentCare);
        Assert.Equal(85, result.ConfidenceScore);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithEmptyText_ReturnsDefaultResponse()
    {
        // Arrange
        var request = new TicketTriageRequest
        {
            TicketText = "",
            Context = null,
            RequestedPriority = null
        };

        // Act
        var result = await _service.AnalyzeTicketAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("General", result.Category);
        Assert.False(string.IsNullOrEmpty(result.SuggestedReply));
    }
}