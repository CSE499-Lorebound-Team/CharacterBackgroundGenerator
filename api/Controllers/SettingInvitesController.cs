using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Common;
using Lorebound.Api.Dtos.Invites;
using Lorebound.Api.Errors;
using Lorebound.Api.Mapping;
using Lorebound.Api.Models;
using Lorebound.Api.Sharing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lorebound.Api.Controllers;

/// <summary>
/// A GameMaster's invite codes for one setting: create (P3-02), list and
/// revoke (P3-03). Players never see codes.
/// </summary>
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

  /// <summary>Every invite of the setting, newest first, with its current status.</summary>
  [HttpGet]
  [ProducesResponseType<PagedResult<InviteDto>>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PagedResult<InviteDto>>> List(
      Guid settingId,
      [FromQuery] PageQuery pageQuery,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireGameMasterAsync(settingId, cancellationToken);

    var now = _timeProvider.GetUtcNow();

    var query = _db.SettingInvites
        .AsNoTracking()
        .Where(invite => invite.CampaignSettingId == settingId);

    var totalCount = await query.CountAsync(cancellationToken);

    var invites = await query
        .OrderByDescending(invite => invite.CreatedAt)
        .ThenBy(invite => invite.Id)
        .Skip(pageQuery.Skip)
        .Take(pageQuery.PageSize)
        .ToListAsync(cancellationToken);

    return Ok(new PagedResult<InviteDto>(
        invites
            .Select(invite => invite.ToDto(_links.JoinSetting(invite.Code), now))
            .ToList(),
        pageQuery.Page,
        pageQuery.PageSize,
        totalCount));
  }

  /// <summary>
  /// Revokes an invite so it can no longer be accepted. The row is kept;
  /// revoking twice is a no-op 204.
  /// </summary>
  [HttpDelete("{inviteId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Revoke(
      Guid settingId,
      Guid inviteId,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireGameMasterAsync(settingId, cancellationToken);

    // Scoped to the setting, so a GM of one setting cannot revoke another's.
    var invite = await _db.SettingInvites
        .SingleOrDefaultAsync(
            invite => invite.Id == inviteId && invite.CampaignSettingId == settingId,
            cancellationToken)
        ?? throw new NotFoundException("Invite not found.");

    if (invite.RevokedAt is null)
    {
      invite.RevokedAt = _timeProvider.GetUtcNow();
      await _db.SaveChangesAsync(cancellationToken);
    }

    return NoContent();
  }

  private static bool IsCodeCollision(DbUpdateException error) =>
      error.InnerException is PostgresException
      {
        SqlState: PostgresErrorCodes.UniqueViolation,
        ConstraintName: "IX_SettingInvites_Code",
      };
}
