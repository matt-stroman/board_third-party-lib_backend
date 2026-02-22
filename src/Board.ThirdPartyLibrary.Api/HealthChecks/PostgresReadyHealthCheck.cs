using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Board.ThirdPartyLibrary.Api.HealthChecks;

/// <summary>
/// Health check that reports API readiness based on PostgreSQL connectivity.
/// </summary>
public sealed class PostgresReadyHealthCheck : IHealthCheck
{
    private readonly IPostgresReadinessProbe _probe;
    private readonly ILogger<PostgresReadyHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresReadyHealthCheck"/> class.
    /// </summary>
    /// <param name="probe">The PostgreSQL readiness probe abstraction.</param>
    /// <param name="logger">The logger.</param>
    public PostgresReadyHealthCheck(
        IPostgresReadinessProbe probe,
        ILogger<PostgresReadyHealthCheck> logger)
    {
        _probe = probe;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _probe.ProbeAsync(cancellationToken);

            if (!result.IsHealthy)
            {
                return HealthCheckResult.Unhealthy(
                    result.FailureDescription ?? "PostgreSQL readiness probe reported an unhealthy state.");
            }

            return HealthCheckResult.Healthy(
                description: "PostgreSQL connection succeeded.",
                data: new Dictionary<string, object>
                {
                    ["database"] = result.Database ?? string.Empty,
                    ["user"] = result.User ?? string.Empty
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL readiness check failed.");
            return HealthCheckResult.Unhealthy("PostgreSQL connection failed.", ex);
        }
    }
}
