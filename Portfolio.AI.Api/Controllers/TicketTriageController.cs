using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Models;
using Portfolio.AI.Api.Services;

namespace Portfolio.AI.Api.Controllers;

/// <summary>
/// Controller for AI-powered ticket triage analysis.
/// Accepts ticket text and returns categorized analysis with suggested replies.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("ticket-triage")]
public class TicketTriageController : ControllerBase
{
    private readonly ITicketTriageService _triageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketTriageController"/> class.
    /// </summary>
    /// <param name="triageService">The ticket triage service for analysis.</param>
    public TicketTriageController(ITicketTriageService triageService)
    {
        _triageService = triageService;
    }

    /// <summary>
    /// Analyzes a ticket and returns triage recommendations.
    /// </summary>
    /// <param name="request">The ticket triage request containing text and optional context.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A triage response with category, suggested reply, and priority.</returns>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(TicketTriageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<TicketTriageResponse>> AnalyzeTicketAsync(
        [FromBody] TicketTriageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TicketText))
        {
            return BadRequest("Ticket text is required.");
        }

        try
        {
            var response = await _triageService.AnalyzeTicketAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (AiProviderException exception)
        {
            return CreateProviderProblem(exception);
        }
    }

    /// <summary>
    /// Quick triage endpoint that accepts just the ticket text.
    /// </summary>
    /// <param name="ticketText">The ticket text to analyze.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A triage response with category and suggested reply.</returns>
    [HttpPost("quick")]
    [ProducesResponseType(typeof(TicketTriageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<TicketTriageResponse>> QuickTriage(
        [FromBody] string ticketText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticketText))
        {
            return BadRequest("Ticket text is required.");
        }

        var request = new TicketTriageRequest
        {
            TicketText = ticketText
        };

        try
        {
            var response = await _triageService.AnalyzeTicketAsync(request, cancellationToken);
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
                (StatusCodes.Status400BadRequest, "Invalid triage request.", "ai_provider_invalid_request"),
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
