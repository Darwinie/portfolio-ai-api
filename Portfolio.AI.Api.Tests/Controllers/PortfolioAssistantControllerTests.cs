using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Controllers;
using Portfolio.AI.Api.Models;
using Portfolio.AI.Api.Services;
using Portfolio.AI.Api.Tests.AI;

namespace Portfolio.AI.Api.Tests.Controllers;

public class PortfolioAssistantControllerTests
{
    private readonly MockPortfolioAssistantProvider _provider = new();
    private readonly FakePortfolioKnowledgeSource _knowledge = new();

    [Fact]
    public async Task ChatAsync_WithValidRequest_ReturnsOkResult()
    {
        _provider.Response = new PortfolioAssistantResponse
        {
            Answer = "Darwin has experience with C#, .NET, Vue.js, SQL Server, and automation.",
            FoundInKnowledgeBase = true
        };

        var controller = CreateController();

        var result = await controller.ChatAsync(new PortfolioAssistantRequest
        {
            Question = "What technologies does Darwin use?"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PortfolioAssistantResponse>(ok.Value);
        Assert.Contains("C#", response.Answer);
        Assert.True(response.FoundInKnowledgeBase);
        Assert.Equal(1, _provider.CallCount);
    }

    [Fact]
    public async Task ChatAsync_WithEmptyQuestion_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.ChatAsync(new PortfolioAssistantRequest
        {
            Question = "  "
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, _provider.CallCount);
    }

    [Fact]
    public async Task ChatAsync_WithNullRequest_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.ChatAsync(null!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, _provider.CallCount);
    }

    [Fact]
    public async Task ChatAsync_WhenProviderIsRateLimited_ReturnsSafeProblem()
    {
        _provider.ExceptionToThrow = new AiProviderException(
            AiProviderFailureKind.RateLimited,
            "The AI provider quota has been exhausted. Please try again later.");

        var controller = CreateController();

        var result = await controller.ChatAsync(new PortfolioAssistantRequest
        {
            Question = "Tell me about Darwin."
        });

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("ai_provider_rate_limited", problem.Extensions["code"]);
        Assert.DoesNotContain("quota detail", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Controller_HasAssistantRateLimitAndRequestSizeLimit()
    {
        var rateLimit = typeof(PortfolioAssistantController)
            .GetCustomAttribute<EnableRateLimitingAttribute>();
        var requestSizeLimit = typeof(PortfolioAssistantController)
            .GetMethod(nameof(PortfolioAssistantController.ChatAsync))!
            .GetCustomAttribute<RequestSizeLimitAttribute>();

        Assert.NotNull(rateLimit);
        Assert.Equal("portfolio-assistant", rateLimit.PolicyName);
        Assert.NotNull(requestSizeLimit);
    }

    [Fact]
    public void TicketTriageController_UsesItsOwnRateLimit()
    {
        var rateLimit = typeof(TicketTriageController)
            .GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(rateLimit);
        Assert.Equal("ticket-triage", rateLimit.PolicyName);
    }

    private PortfolioAssistantController CreateController() =>
        new(new PortfolioAssistantService(_provider, _knowledge));
}
