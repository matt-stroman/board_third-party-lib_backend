namespace Board.ThirdPartyLibrary.Api.HealthChecks;

/// <summary>
/// Abstraction for probing PostgreSQL readiness for the API.
/// </summary>
public interface IPostgresReadinessProbe
{
    /// <summary>
    /// Probes PostgreSQL and returns a readiness result.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the probe.</param>
    /// <returns>The readiness probe result.</returns>
    Task<PostgresReadinessProbeResult> ProbeAsync(CancellationToken cancellationToken = default);
}
