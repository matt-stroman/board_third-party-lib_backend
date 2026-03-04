namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Persisted conversation message authored by an application user.
/// </summary>
internal sealed class ConversationMessage
{
    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the conversation thread identifier.
    /// </summary>
    public Guid ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the authoring user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the author role for display and authorization context.
    /// </summary>
    public string AuthorRole { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the semantic message kind.
    /// </summary>
    public string MessageKind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning conversation thread.
    /// </summary>
    public ConversationThread Thread { get; set; } = null!;

    /// <summary>
    /// Gets or sets the authoring user.
    /// </summary>
    public AppUser User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the attachments uploaded with this message.
    /// </summary>
    public ICollection<ConversationMessageAttachment> Attachments { get; set; } = [];
}
