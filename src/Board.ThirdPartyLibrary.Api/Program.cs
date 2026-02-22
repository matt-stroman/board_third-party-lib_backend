using System.Text.Json.Serialization;
using Board.ThirdPartyLibrary.Api.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddSingleton<IPostgresReadinessProbe, NpgsqlPostgresReadinessProbe>();

builder.Services.AddHealthChecks()
    .AddCheck<PostgresReadyHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "board-third-party-lib-backend",
    endpoints = new[] { "/health/live", "/health/ready" }
}));

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponseAsync
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
});

app.Run();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    context.Response.StatusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;

    var response = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            data = entry.Value.Data.Count == 0 ? null : entry.Value.Data
        }),
        durationMs = report.TotalDuration.TotalMilliseconds
    };

    return context.Response.WriteAsJsonAsync(response);
}

/// <summary>
/// Entry point marker for integration and endpoint tests.
/// </summary>
public partial class Program;
