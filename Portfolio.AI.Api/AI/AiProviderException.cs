namespace Portfolio.AI.Api.AI;

/// <summary>
/// Classifies an AI provider failure so callers can map it to a safe HTTP response
/// without exposing provider-specific error details.
/// </summary>
public enum AiProviderFailureKind
{
    /// <summary>The provider is missing required configuration, such as an API key or model.</summary>
    Configuration,

    /// <summary>The provider rejected the configured credentials.</summary>
    Authentication,

    /// <summary>The provider quota has been exhausted.</summary>
    RateLimited,

    /// <summary>The provider did not respond within the configured timeout.</summary>
    Timeout,

    /// <summary>The provider is unavailable or returned a server error.</summary>
    Unavailable,

    /// <summary>The provider returned malformed or invalid structured output.</summary>
    InvalidResponse,

    /// <summary>The request is not valid for the provider.</summary>
    InvalidRequest
}

/// <summary>
/// Exception raised by AI provider or portfolio knowledge implementations.
/// The message is safe to return to API clients; provider response bodies, file contents, and keys must not be included.
/// </summary>
public sealed class AiProviderException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AiProviderException"/> class.
    /// </summary>
    /// <param name="kind">The failure category.</param>
    /// <param name="message">A safe, client-facing message.</param>
    /// <param name="innerException">The optional underlying exception for server-side diagnostics.</param>
    public AiProviderException(
        AiProviderFailureKind kind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>
    /// Gets the failure category.
    /// </summary>
    public AiProviderFailureKind Kind { get; }
}
