using Lorebound.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly LoreboundDbContext _db;

    public HealthController(LoreboundDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseReachable = await _db.Database.CanConnectAsync(cancellationToken);

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
