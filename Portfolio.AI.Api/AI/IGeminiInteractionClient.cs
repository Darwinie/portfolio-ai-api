namespace Portfolio.AI.Api.AI;

/// <summary>
/// Sends requests to the configured Gemini Interactions API and returns
/// validated model output text. Provider-specific prompts and response
/// schemas remain in the individual providers.
/// </summary>
public interface IGeminiInteractionClient
{
    /// <summary>
    /// Sends an interaction request and returns the completed model output text.
    /// </summary>
    /// <exception cref="AiProviderException">
    /// Thrown for missing configuration, provider failures, timeouts, or invalid output.
    /// </exception>
    Task<string> SendInteractionAsync(
        object requestBody,
        CancellationToken cancellationToken = default);
}
