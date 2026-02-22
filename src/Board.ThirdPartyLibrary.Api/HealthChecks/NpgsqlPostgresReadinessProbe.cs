using Npgsql;

namespace Board.ThirdPartyLibrary.Api.HealthChecks;

/// <summary>
/// Npgsql-backed implementation of <see cref="IPostgresReadinessProbe"/>.
/// </summary>
public sealed class NpgsqlPostgresReadinessProbe : IPostgresReadinessProbe
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="NpgsqlPostgresReadinessProbe"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    public NpgsqlPostgresReadinessProbe(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<PostgresReadinessProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("BoardLibrary");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return PostgresReadinessProbeResult.Unhealthy(
                "Connection string 'ConnectionStrings:BoardLibrary' is not configured.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            "SELECT current_database(), current_user;",
            connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return PostgresReadinessProbeResult.Unhealthy("PostgreSQL query returned no rows.");
        }

        return PostgresReadinessProbeResult.Healthy(
            database: reader.GetString(0),
            user: reader.GetString(1));
    }
}
