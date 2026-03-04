namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Application-owned developer enrollment request for a user projection.
/// </summary>
internal sealed class DeveloperEnrollmentRequest
{
    /// <summary>
    /// Gets or sets the developer enrollment request identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the applicant user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the current workflow status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the linked conversation thread identifier.
    /// </summary>
    public Guid ConversationThreadId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the request was submitted.
    /// </summary>
    public DateTime RequestedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the rejection probation period ends.
    /// </summary>
    public DateTime? ReapplyAvailableAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the request was cancelled.
    /// </summary>
    public DateTime? CancelledAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the latest moderator action on this request.
    /// </summary>
    public DateTime? LastModeratorActionAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the user identifier for the latest moderator action on this request.
    /// </summary>
    public Guid? LastModeratorActionByUserId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the request was reviewed.
    /// </summary>
    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the reviewer user identifier when the request has been reviewed.
    /// </summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the linked conversation thread.
    /// </summary>
    public ConversationThread ConversationThread { get; set; } = null!;

    /// <summary>
    /// Gets or sets the applicant user projection.
    /// </summary>
    public AppUser User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user projection for the latest moderator action when one exists.
    /// </summary>
    public AppUser? LastModeratorActionByUser { get; set; }

    /// <summary>
    /// Gets or sets the reviewer user projection when one exists.
    /// </summary>
    public AppUser? ReviewedByUser { get; set; }
}
