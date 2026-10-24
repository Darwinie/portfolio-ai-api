using System.ComponentModel.DataAnnotations;

namespace Portfolio.AI.Api.Models;

/// <summary>
/// Request model for Personal AI Portfolio Assistant chat.
/// </summary>
public class PortfolioAssistantRequest
{
    /// <summary>
    /// Gets or sets the visitor's question.
    /// </summary>
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional bounded conversation history.
    /// </summary>
    [MaxLength(10)]
    public List<PortfolioChatMessage>? History { get; set; }
}

/// <summary>
/// A single message in the conversation history.
/// </summary>
public class PortfolioChatMessage
{
    /// <summary>
    /// Gets or sets the role of the message sender.
    /// </summary>
    [Required]
    [RegularExpression("^(user|assistant)$")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    [Required]
    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;
}
