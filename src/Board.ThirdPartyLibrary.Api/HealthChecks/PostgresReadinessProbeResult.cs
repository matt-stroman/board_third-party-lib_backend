namespace Board.ThirdPartyLibrary.Api.HealthChecks;

/// <summary>
/// Result returned by a PostgreSQL readiness probe.
/// </summary>
public sealed record PostgresReadinessProbeResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresReadinessProbeResult"/> class.
    /// </summary>
    /// <param name="isHealthy">Whether the probe succeeded.</param>
    /// <param name="database">The current database name when successful.</param>
    /// <param name="user">The current database user when successful.</param>
    /// <param name="failureDescription">The failure description when unsuccessful.</param>
    public PostgresReadinessProbeResult(
        bool isHealthy,
        string? database = null,
        string? user = null,
        string? failureDescription = null)
    {
        IsHealthy = isHealthy;
        Database = database;
        User = user;
        FailureDescription = failureDescription;
    }

    /// <summary>
    /// Gets a value indicating whether the probe succeeded.
    /// </summary>
    public bool IsHealthy { get; }

    /// <summary>
    /// Gets the database name returned by PostgreSQL.
    /// </summary>
    public string? Database { get; }

    /// <summary>
    /// Gets the user returned by PostgreSQL.
    /// </summary>
    public string? User { get; }

    /// <summary>
    /// Gets the human-readable failure description when the probe is unhealthy.
    /// </summary>
    public string? FailureDescription { get; }

    /// <summary>
    /// Creates a healthy probe result.
    /// </summary>
    /// <param name="database">The current database name.</param>
    /// <param name="user">The current database user.</param>
    /// <returns>A healthy probe result.</returns>
    public static PostgresReadinessProbeResult Healthy(string database, string user) =>
        new(true, database, user);

    /// <summary>
    /// Creates an unhealthy probe result.
    /// </summary>
    /// <param name="failureDescription">The reason the probe is unhealthy.</param>
    /// <returns>An unhealthy probe result.</returns>
    public static PostgresReadinessProbeResult Unhealthy(string failureDescription) =>
        new(false, failureDescription: failureDescription);
}
