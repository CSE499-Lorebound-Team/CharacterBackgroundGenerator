using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Lorebound.Api.Controllers;

/// <summary>
/// Liveness plus a database check. The check is the registered
/// <c>AddDbContextCheck</c> (P8-05); the JSON shape is unchanged since P0-08:
/// 200 with <c>database: "connected"</c>, or 503 with
/// <c>database: "unreachable"</c> when Postgres is down.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    /// <summary>The name of the database health check.</summary>
    public const string DatabaseCheck = "database";

    private readonly HealthCheckService _healthChecks;

    public HealthController(HealthCheckService healthChecks)
    {
        _healthChecks = healthChecks;
    }

    /// <summary>
    /// Whether the API is up and can reach its database. Public, for load
    /// balancers and uptime checks: 200 with <c>database: "connected"</c>,
    /// or 503 with <c>database: "unreachable"</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await _healthChecks.CheckHealthAsync(cancellationToken);
        var databaseReachable =
            report.Entries.TryGetValue(DatabaseCheck, out var database)
            && database.Status == HealthStatus.Healthy;

        var body = new HealthResponse(
            databaseReachable ? "healthy" : "unhealthy",
            "Lorebound API",
            databaseReachable ? "connected" : "unreachable");

        return databaseReachable
            ? Ok(body)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}

/// <summary>
/// <c>status</c> is <c>healthy</c> or <c>unhealthy</c>; <c>database</c> is
/// <c>connected</c> or <c>unreachable</c>.
/// </summary>
public record HealthResponse(string Status, string Application, string Database);
