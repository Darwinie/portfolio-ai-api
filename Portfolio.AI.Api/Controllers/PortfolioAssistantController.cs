using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Models;
using Portfolio.AI.Api.Services;

namespace Portfolio.AI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("portfolio-assistant")]
public class PortfolioAssistantController : ControllerBase
{
    private readonly IPortfolioAssistantService _assistantService;

    public PortfolioAssistantController(IPortfolioAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    [HttpPost("chat")]
    [RequestSizeLimit(32 * 1024)]
    [ProducesResponseType(typeof(PortfolioAssistantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PortfolioAssistantResponse>> ChatAsync(
        [FromBody] PortfolioAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Question))
        {
            return BadRequest("Question is required.");
        }

        try
        {
            var response = await _assistantService.ChatAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (AiProviderException exception)
        {
            return CreateProviderProblem(exception);
        }
    }

    private ObjectResult CreateProviderProblem(AiProviderException exception)
    {
        var (statusCode, title, code) = exception.Kind switch
        {
            AiProviderFailureKind.InvalidRequest =>
                (StatusCodes.Status400BadRequest, "Invalid request.", "ai_provider_invalid_request"),
            AiProviderFailureKind.Configuration or AiProviderFailureKind.Authentication =>
                (StatusCodes.Status503ServiceUnavailable, "AI provider is not configured.", "ai_provider_configuration"),
            AiProviderFailureKind.RateLimited =>
                (StatusCodes.Status503ServiceUnavailable, "AI provider quota exceeded.", "ai_provider_rate_limited"),
            AiProviderFailureKind.Timeout =>
                (StatusCodes.Status504GatewayTimeout, "AI provider timed out.", "ai_provider_timeout"),
            AiProviderFailureKind.Unavailable =>
                (StatusCodes.Status502BadGateway, "AI provider is unavailable.", "ai_provider_unavailable"),
            AiProviderFailureKind.InvalidResponse =>
                (StatusCodes.Status502BadGateway, "AI provider returned an invalid response.", "ai_provider_invalid_response"),
            _ =>
                (StatusCodes.Status502BadGateway, "AI provider request failed.", "ai_provider_failure")
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message
        };
        problem.Extensions["code"] = code;

        return StatusCode(statusCode, problem);
    }
}
