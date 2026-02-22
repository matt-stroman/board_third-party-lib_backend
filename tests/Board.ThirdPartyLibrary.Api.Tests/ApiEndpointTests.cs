using System.Net;
using System.Text.Json;
using Board.ThirdPartyLibrary.Api.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Board.ThirdPartyLibrary.Api.Tests;

/// <summary>
/// Endpoint tests for the minimal API surface exposed by the backend service.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ApiEndpointTests
{
    /// <summary>
    /// Verifies the root endpoint returns service metadata and known health endpoints.
    /// </summary>
    [Fact]
    public async Task RootEndpoint_ReturnsServiceMetadata()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("board-third-party-lib-backend", root.GetProperty("service").GetString());
        Assert.Contains(
            root.GetProperty("endpoints").EnumerateArray().Select(element => element.GetString()),
            endpoint => endpoint == "/health/live");
        Assert.Contains(
            root.GetProperty("endpoints").EnumerateArray().Select(element => element.GetString()),
            endpoint => endpoint == "/health/ready");
    }

    /// <summary>
    /// Verifies the liveness endpoint reports healthy without dependency checks.
    /// </summary>
    [Fact]
    public async Task LiveHealthEndpoint_ReturnsHealthyWithoutDependencyChecks()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.Empty(root.GetProperty("checks").EnumerateArray());
    }

    /// <summary>
    /// Verifies liveness remains healthy even if the readiness probe would fail.
    /// </summary>
    [Fact]
    public async Task LiveHealthEndpoint_IgnoresReadinessProbeFailures()
    {
        using var factory = new TestApiFactory(
            _ => throw new InvalidOperationException("Probe should not be invoked by liveness endpoint."));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
    }

    /// <summary>
    /// Verifies readiness reports healthy and includes database/user metadata on success.
    /// </summary>
    [Fact]
    public async Task ReadyHealthEndpoint_WhenProbeSucceeds_ReturnsHealthy()
    {
        using var factory = new TestApiFactory(
            _ => Task.FromResult(PostgresReadinessProbeResult.Healthy("board_tpl", "board_tpl_user")));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());

        var postgresCheck = root.GetProperty("checks")
            .EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "postgres");

        Assert.Equal("Healthy", postgresCheck.GetProperty("status").GetString());
        Assert.Equal(
            "board_tpl",
            postgresCheck.GetProperty("data").GetProperty("database").GetString());
        Assert.Equal(
            "board_tpl_user",
            postgresCheck.GetProperty("data").GetProperty("user").GetString());
    }

    /// <summary>
    /// Verifies readiness returns service unavailable when the probe reports an unhealthy result.
    /// </summary>
    [Fact]
    public async Task ReadyHealthEndpoint_WhenProbeReportsUnhealthy_ReturnsServiceUnavailable()
    {
        using var factory = new TestApiFactory(
            _ => Task.FromResult(PostgresReadinessProbeResult.Unhealthy("Configured test failure.")));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());

        var postgresCheck = root.GetProperty("checks")
            .EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "postgres");

        Assert.Equal("Unhealthy", postgresCheck.GetProperty("status").GetString());
        Assert.Equal("Configured test failure.", postgresCheck.GetProperty("description").GetString());
    }

    /// <summary>
    /// Verifies readiness returns service unavailable when the probe throws unexpectedly.
    /// </summary>
    [Fact]
    public async Task ReadyHealthEndpoint_WhenProbeThrows_ReturnsServiceUnavailable()
    {
        using var factory = new TestApiFactory(
            _ => throw new InvalidOperationException("Boom"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());

        var postgresCheck = root.GetProperty("checks")
            .EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "postgres");

        Assert.Equal("Unhealthy", postgresCheck.GetProperty("status").GetString());
        Assert.Equal("PostgreSQL connection failed.", postgresCheck.GetProperty("description").GetString());
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly Func<CancellationToken, Task<PostgresReadinessProbeResult>>? _probeFunc;

        public TestApiFactory(Func<CancellationToken, Task<PostgresReadinessProbeResult>>? probeFunc = null)
        {
            _probeFunc = probeFunc;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            if (_probeFunc is null)
            {
                return;
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPostgresReadinessProbe>();
                services.AddSingleton<IPostgresReadinessProbe>(new FakePostgresReadinessProbe(_probeFunc));
            });
        }
    }

    private sealed class FakePostgresReadinessProbe : IPostgresReadinessProbe
    {
        private readonly Func<CancellationToken, Task<PostgresReadinessProbeResult>> _probeFunc;

        public FakePostgresReadinessProbe(Func<CancellationToken, Task<PostgresReadinessProbeResult>> probeFunc)
        {
            _probeFunc = probeFunc;
        }

        public Task<PostgresReadinessProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
            _probeFunc(cancellationToken);
    }
}
