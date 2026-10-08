using Lorebound.Api.Auth;
using Lorebound.Api.Dtos.Users;
using Lorebound.Api.Errors;
using Lorebound.Api.Mapping;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Controllers;

/// <summary>
/// The signed-in user's own profile. Signed-in only (fallback policy).
/// </summary>
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
  private readonly UserManager<ApplicationUser> _userManager;
  private readonly ICurrentUser _currentUser;

  public UsersController(UserManager<ApplicationUser> userManager, ICurrentUser currentUser)
  {
    _userManager = userManager;
    _currentUser = currentUser;
  }

  [HttpGet("me")]
  [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<UserProfileDto> GetMe() =>
      (await FindCurrentUserAsync()).ToProfileDto();

  /// <summary>
  /// Changes the display name (1-60 characters, trimmed). Email and password
  /// changes are out of scope for now.
  /// </summary>
  [HttpPut("me")]
  [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
  {
    var user = await FindCurrentUserAsync();
    user.DisplayName = request.DisplayName!.Trim();

    // Does not change the security stamp, so the session stays valid; the
    // display name claim is rebuilt from the database on the next request.
    var result = await _userManager.UpdateAsync(user);
    if (!result.Succeeded)
    {
      foreach (var error in result.Errors)
      {
        ModelState.AddModelError(string.Empty, error.Description);
      }

      return ValidationProblem(ModelState);
    }

    return Ok(user.ToProfileDto());
  }

  private async Task<ApplicationUser> FindCurrentUserAsync() =>
      await _userManager.FindByIdAsync(_currentUser.UserId.ToString())
      ?? throw new NotFoundException("User not found.");
}
