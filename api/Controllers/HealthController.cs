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

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await _healthChecks.CheckHealthAsync(cancellationToken);
        var databaseReachable =
            report.Entries.TryGetValue(DatabaseCheck, out var database)
            && database.Status == HealthStatus.Healthy;

        var body = new
        {
            status = databaseReachable ? "healthy" : "unhealthy",
            application = "Lorebound API",
            database = databaseReachable ? "connected" : "unreachable"
        };

        return databaseReachable
            ? Ok(body)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
