namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Generic persisted conversation thread that can be attached to domain workflows.
/// </summary>
internal sealed class ConversationThread
{
    /// <summary>
    /// Gets or sets the conversation thread identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the messages in this conversation.
    /// </summary>
    public ICollection<ConversationMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional developer enrollment request linked to this thread.
    /// </summary>
    public DeveloperEnrollmentRequest? DeveloperEnrollmentRequest { get; set; }
}
