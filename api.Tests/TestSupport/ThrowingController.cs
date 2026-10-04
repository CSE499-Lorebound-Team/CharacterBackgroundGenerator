using System.ComponentModel.DataAnnotations;
using Lorebound.Api.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Tests.TestSupport;

[ApiController]
[AllowAnonymous]
[Route("test/errors")]
public class ThrowingController : ControllerBase
{
  [HttpGet("not-found")]
  public IActionResult ThrowNotFound() => throw new NotFoundException("Setting not found.");

  [HttpGet("forbidden")]
  public IActionResult ThrowForbidden() => throw new ForbiddenException();

  [HttpGet("conflict")]
  public IActionResult ThrowConflict() => throw new ConflictException();

  [HttpGet("unhandled")]
  public IActionResult ThrowUnhandled() =>
      throw new InvalidOperationException("secret internal detail");

  [HttpPost("validate")]
  public IActionResult Validate(ValidatedRequest request) => NoContent();

  public record ValidatedRequest(
      [Required, MaxLength(10)] string? Name);
}
