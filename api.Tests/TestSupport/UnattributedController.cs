using Lorebound.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Tests.TestSupport;

/// <summary>
/// No [Authorize] or [AllowAnonymous]: proves the fallback policy protects a
/// controller nobody remembered to secure, and exposes ICurrentUser.
/// </summary>
[ApiController]
[Route("test/unattributed")]
public class UnattributedController : ControllerBase
{
  [HttpGet]
  public IActionResult Get() => NoContent();

  [HttpGet("me")]
  public object Me([FromServices] ICurrentUser currentUser) => new
  {
    currentUser.UserId,
    currentUser.Email,
    currentUser.DisplayName,
  };
}
