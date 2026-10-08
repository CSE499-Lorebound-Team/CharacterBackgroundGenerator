using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Invites;
using Lorebound.Api.Errors;
using Lorebound.Api.Security;
using Lorebound.Api.Sharing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Controllers;

/// <summary>
/// Invites seen from the joining side, by code rather than by setting (P3-04).
/// Signed-in users only. Every unusable code (unknown, malformed, expired,
/// revoked or used up) gets the same 404, so nobody can tell a dead code from
/// one that never existed; the rate limit slows down guessing.
/// </summary>
[ApiController]
[Route("api/invites")]
[EnableRateLimiting(RateLimitingSetup.InvitePolicy)]
public class InvitesController : ControllerBase
{
  private const string NotFoundMessage = "Invite not found.";

  private readonly LoreboundDbContext _db;
  private readonly ICurrentUser _currentUser;
  private readonly TimeProvider _timeProvider;

  public InvitesController(
      LoreboundDbContext db,
      ICurrentUser currentUser,
      TimeProvider timeProvider)
  {
    _db = db;
    _currentUser = currentUser;
    _timeProvider = timeProvider;
  }

  /// <summary>
  /// What the user would join. Reading a preview never uses up the invite.
  /// </summary>
  [HttpGet("{code}")]
  [ProducesResponseType<InvitePreviewDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
  public async Task<ActionResult<InvitePreviewDto>> Preview(
      string code,
      CancellationToken cancellationToken)
  {
    if (!InviteCodes.TryNormalize(code, out var normalized))
    {
      throw new NotFoundException(NotFoundMessage);
    }

    var userId = _currentUser.UserId;

    var preview = await _db.SettingInvites
        .AsNoTracking()
        .Where(invite => invite.Code == normalized)
        .Where(InviteRules.IsActiveAt(_timeProvider.GetUtcNow()))
        .Select(invite => new InvitePreviewDto(
            invite.CampaignSetting.Name,
            invite.CampaignSetting.Owner.DisplayName,
            invite.CampaignSetting.OwnerUserId == userId
                || invite.CampaignSetting.Memberships.Any(m => m.UserId == userId)))
        .SingleOrDefaultAsync(cancellationToken);

    return preview ?? throw new NotFoundException(NotFoundMessage);
  }
}
