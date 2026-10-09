using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Users;
using Lorebound.Api.Errors;
using Lorebound.Api.Mapping;
using Lorebound.Api.Models;
using Lorebound.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Controllers;

/// <summary>
/// The signed-in user's own profile. Signed-in only (fallback policy).
/// </summary>
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
  private readonly UserManager<ApplicationUser> _userManager;
  private readonly SignInManager<ApplicationUser> _signInManager;
  private readonly LoreboundDbContext _db;
  private readonly ICurrentUser _currentUser;
  private readonly ILogger<UsersController> _logger;

  public UsersController(
      UserManager<ApplicationUser> userManager,
      SignInManager<ApplicationUser> signInManager,
      LoreboundDbContext db,
      ICurrentUser currentUser,
      ILogger<UsersController> logger)
  {
    _userManager = userManager;
    _signInManager = signInManager;
    _db = db;
    _currentUser = currentUser;
    _logger = logger;
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

  /// <summary>
  /// Deletes the account after re-checking the password (400 keyed
  /// <c>Password</c> when wrong; rate limited like login). Blocked with 409
  /// and the list of settings while the user owns a setting that has other
  /// members: there is no ownership transfer, so they must delete the
  /// setting or remove its members first. Otherwise, in one transaction:
  /// the settings they own (with everything in them), their memberships,
  /// characters and invites elsewhere, then the user. Signs out; 204.
  /// </summary>
  [HttpDelete("me")]
  [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> DeleteMe(
      DeleteAccountRequest request,
      CancellationToken cancellationToken)
  {
    var user = await FindCurrentUserAsync();

    if (!await _userManager.CheckPasswordAsync(user, request.Password!))
    {
      ModelState.AddModelError(nameof(request.Password), "The password is incorrect.");
      return ValidationProblem(ModelState);
    }

    var userId = user.Id;

    await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

    // Checked inside the transaction, right before deleting.
    var blocking = await _db.CampaignSettings
        .Where(s => s.OwnerUserId == userId
            && s.Memberships.Any(m => m.UserId != userId))
        .OrderBy(s => s.Name)
        .Select(s => new BlockingSettingDto(
            s.Id,
            s.Name,
            s.Memberships.Count(m => m.UserId != userId)))
        .ToListAsync(cancellationToken);

    if (blocking.Count > 0)
    {
      throw new ConflictException(
          "You own settings that other people still belong to. Delete those settings " +
          "or remove their members before deleting your account; ownership cannot be " +
          "transferred.",
          new Dictionary<string, object?> { ["settings"] = blocking });
    }

    // Owned settings go with everything in them (entries, relationships,
    // memberships, invites, and characters, including ones left behind by
    // removed players), through the database cascade.
    await _db.CampaignSettings
        .Where(s => s.OwnerUserId == userId)
        .ExecuteDeleteAsync(cancellationToken);

    await _db.SettingMemberships
        .Where(m => m.UserId == userId)
        .ExecuteDeleteAsync(cancellationToken);

    await _db.Characters
        .Where(c => c.OwnerUserId == userId)
        .ExecuteDeleteAsync(cancellationToken);

    await _db.SettingInvites
        .Where(i => i.CreatedByUserId == userId)
        .ExecuteDeleteAsync(cancellationToken);

    var result = await _userManager.DeleteAsync(user);
    if (!result.Succeeded)
    {
      throw new InvalidOperationException(
          "Could not delete user: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    await transaction.CommitAsync(cancellationToken);

    _logger.LogInformation("User {UserId} deleted their account.", userId);

    await _signInManager.SignOutAsync();
    return NoContent();
  }

  private async Task<ApplicationUser> FindCurrentUserAsync() =>
      await _userManager.FindByIdAsync(_currentUser.UserId.ToString())
      ?? throw new NotFoundException("User not found.");
}
