using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Tests.TestSupport;

/// <summary>
/// Exercises the auth cookie without a database or the real login endpoint
/// (P1-03): sign-in issues the cookie for a made-up user with no roles.
/// </summary>
[ApiController]
[Route("test/auth")]
public class AuthProbeController : ControllerBase
{
  [AllowAnonymous]
  [HttpPost("sign-in")]
  public async Task<IActionResult> SignIn(bool persistent = false)
  {
    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
        IdentityConstants.ApplicationScheme);

    await HttpContext.SignInAsync(
        IdentityConstants.ApplicationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = persistent });

    return NoContent();
  }

  [Authorize]
  [HttpGet("protected")]
  public IActionResult Protected() => NoContent();

  [Authorize(Roles = "Admin")]
  [HttpGet("admin-only")]
  public IActionResult AdminOnly() => NoContent();
}
