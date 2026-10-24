using Microsoft.AspNetCore.Mvc;
using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Controllers;
using Portfolio.AI.Api.Models;
using Portfolio.AI.Api.Services;
using Portfolio.AI.Api.Tests.AI;

namespace Portfolio.AI.Api.Tests.Controllers;

/// <summary>
/// Unit tests for the TicketTriageController class.
/// Uses mocked AI provider to test controller actions without API costs.
/// </summary>
public class TicketTriageControllerTests
{
    private readonly MockAiProvider _mockProvider;
    private readonly TicketTriageService _service;

    public TicketTriageControllerTests()
    {
        _mockProvider = new MockAiProvider();
        _service = new TicketTriageService(_mockProvider, new AiProviderOptions());
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new TicketTriageRequest
        {
            TicketText = "Application crashes on startup",
            Context = "Production environment",
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
        var controller = new TicketTriageController(_service);
        var result = await controller.AnalyzeTicketAsync(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TicketTriageResponse>(okResult.Value);
        Assert.Equal("Bug", response.Category);
        Assert.Equal("We'll investigate the crash issue.", response.SuggestedReply);
        Assert.Equal("High", response.AdjustedPriority);
        Assert.True(response.RequiresUrgentCare);
        Assert.Equal(90, response.ConfidenceScore);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithNullText_ReturnsBadRequest()
    {
        // Arrange
        var request = new TicketTriageRequest
        {
            TicketText = null!
        };

        // Act
        var controller = new TicketTriageController(_service);
        var result = await controller.AnalyzeTicketAsync(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Ticket text is required.", badRequestResult.Value);
    }

    [Fact]
    public async Task AnalyzeTicketAsync_WithEmptyText_ReturnsBadRequest()
    {
        // Arrange
        var request = new TicketTriageRequest
        {
            TicketText = string.Empty
        };

        // Act
        var controller = new TicketTriageController(_service);
        var result = await controller.AnalyzeTicketAsync(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Ticket text is required.", badRequestResult.Value);
    }

    [Fact]
    public async Task QuickTriage_WithValidText_ReturnsOkResult()
    {
        // Arrange
        var ticketText = "How do I fix error 500?";

        _mockProvider.MockResponse = new TicketTriageResponse
        {
            Category = "Question",
            SuggestedReply = "Please consult the documentation or contact support.",
            AdjustedPriority = "Medium",
            RequiresUrgentCare = false,
            ConfidenceScore = 80
        };

        // Act
        var controller = new TicketTriageController(_service);
        var result = await controller.QuickTriage(ticketText);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TicketTriageResponse>(okResult.Value);
        Assert.Equal("Question", response.Category);
        Assert.Equal("Please consult the documentation or contact support.", response.SuggestedReply);
    }

    [Fact]
    public async Task QuickTriage_WithNullText_ReturnsBadRequest()
    {
        // Arrange
        string? ticketText = null;

        // Act
        var controller = new TicketTriageController(_service);
        var result = await controller.QuickTriage(ticketText!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Ticket text is required.", badRequestResult.Value);
    }

    [Fact]
    public async Task QuickTriage_WithEmptyText_ReturnsBadRequest()
    {
        // Arrange
        var ticketText = string.Empty;

        // Act
        var controller = new TicketTriageController(_service);
        var result = await controller.QuickTriage(ticketText);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Ticket text is required.", badRequestResult.Value);
    }
}