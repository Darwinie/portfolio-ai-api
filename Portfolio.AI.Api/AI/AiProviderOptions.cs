namespace Portfolio.AI.Api.AI;

/// <summary>
/// Strongly typed options for the AI triage provider configuration.
/// Read from appsettings.json, User Secrets (development), or environment/secret configuration (deployment).
/// </summary>
public class AiProviderOptions
{
    /// <summary>
    /// Gets or sets the Gemini API key. Never commit this value to source control.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Gemini base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";

    /// <summary>
    /// Gets or sets the model name to use (for example, "gemini-3.5-flash-lite").
    /// </summary>
    public string ModelName { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>
    /// Gets or sets the maximum time to wait for a provider response.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of characters accepted for the ticket text.
    /// </summary>
    public int MaxTicketCharacters { get; set; } = 8000;

    /// <summary>
    /// Gets or sets the maximum number of characters accepted for optional context.
    /// </summary>
    public int MaxContextCharacters { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the maximum number of output tokens requested from the provider.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the Gemini thinking level (for example, "low" or "high").
    /// Set to null or empty to use the provider default.
    /// </summary>
    public string? ThinkingLevel { get; set; } = "low";

    /// <summary>
    /// Gets or sets the maximum number of bytes accepted in a provider response.
    /// </summary>
    public int MaxResponseBytes { get; set; } = 131072;

    /// <summary>
    /// Gets or sets a value indicating whether to adjust priority based on ticket context.
    /// </summary>
    public bool AdjustPriorityBasedOnContext { get; set; }

    /// <summary>
    /// Gets or sets the default priority level if analysis is inconclusive.
    /// </summary>
    public string DefaultPriority { get; set; } = "Medium";
}
