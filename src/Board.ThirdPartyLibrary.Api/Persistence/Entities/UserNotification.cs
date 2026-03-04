namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Generic in-app notification delivered to a local user projection.
/// </summary>
internal sealed class UserNotification
{
    /// <summary>
    /// Gets or sets the notification identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the target user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the generic category code.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification body text.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client-facing route to open when the notification is selected.
    /// </summary>
    public string? ActionUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the notification has been read.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the notification was read.
    /// </summary>
    public DateTime? ReadAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the target user projection.
    /// </summary>
    public AppUser User { get; set; } = null!;
}
