namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Optional Board profile linkage cached against an application user.
/// </summary>
internal sealed class UserBoardProfile
{
    /// <summary>
    /// Gets or sets the owning application user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the linked Board user identifier.
    /// </summary>
    public string BoardUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cached Board display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cached Board avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the profile was first linked.
    /// </summary>
    public DateTime LinkedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent sync from Board profile input.
    /// </summary>
    public DateTime LastSyncedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning application user projection.
    /// </summary>
    public AppUser User { get; set; } = null!;
}
