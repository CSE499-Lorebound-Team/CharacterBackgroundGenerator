using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Invites;
using Lorebound.Api.Errors;
using Lorebound.Api.Mapping;
using Lorebound.Api.Models;
using Lorebound.Api.Sharing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lorebound.Api.Controllers;

/// <summary>A GameMaster's invite codes for one setting (P3-02).</summary>
[ApiController]
[Route("api/settings/{settingId:guid}/invites")]
public class SettingInvitesController : ControllerBase
{
  // Two random 10-character codes colliding is vanishingly rare; a few
  // retries only guard against it, they never loop in practice.
  private const int MaxCodeAttempts = 3;

  private readonly LoreboundDbContext _db;
  private readonly ICurrentUser _currentUser;
  private readonly ISettingAccess _settingAccess;
  private readonly FrontendLinks _links;
  private readonly TimeProvider _timeProvider;

  public SettingInvitesController(
      LoreboundDbContext db,
      ICurrentUser currentUser,
      ISettingAccess settingAccess,
      FrontendLinks links,
      TimeProvider timeProvider)
  {
    _db = db;
    _currentUser = currentUser;
    _settingAccess = settingAccess;
    _links = links;
    _timeProvider = timeProvider;
  }

  /// <summary>
  /// Creates a Player invite. The body is optional; expiry defaults to
  /// 7 days and uses to unlimited.
  /// </summary>
  [HttpPost]
  [ProducesResponseType<InviteDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<InviteDto>> Create(
      Guid settingId,
      CreateInviteRequest? request,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireGameMasterAsync(settingId, cancellationToken);

    var now = _timeProvider.GetUtcNow();

    // A soft cap: two simultaneous creates can both pass it, which is fine.
    var activeCount = await _db.SettingInvites
        .Where(invite => invite.CampaignSettingId == settingId)
        .Where(InviteRules.IsActiveAt(now))
        .CountAsync(cancellationToken);

    if (activeCount >= InviteRules.MaxActivePerSetting)
    {
      throw new ConflictException(
          $"This setting already has {InviteRules.MaxActivePerSetting} active invites. " +
          "Revoke one before creating another.");
    }

    var invite = new SettingInvite
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = settingId,
      CreatedByUserId = _currentUser.UserId,
      ExpiresAt = now.AddDays(request?.ExpiresInDays ?? InviteRules.DefaultExpiresInDays),
      MaxUses = request?.MaxUses,
    };

    for (var attempt = 1; ; attempt++)
    {
      invite.Code = InviteCodes.Generate();
      _db.SettingInvites.Add(invite);

      try
      {
        await _db.SaveChangesAsync(cancellationToken);
        break;
      }
      catch (DbUpdateException error)
          when (attempt < MaxCodeAttempts && IsCodeCollision(error))
      {
        _db.Entry(invite).State = EntityState.Detached;
      }
    }

    return StatusCode(
        StatusCodes.Status201Created,
        invite.ToDto(_links.JoinSetting(invite.Code), now));
  }

  private static bool IsCodeCollision(DbUpdateException error) =>
      error.InnerException is PostgresException
      {
        SqlState: PostgresErrorCodes.UniqueViolation,
        ConstraintName: "IX_SettingInvites_Code",
      };
}
