using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Invites;
using Lorebound.Api.Errors;
using Lorebound.Api.Models;
using Lorebound.Api.Security;
using Lorebound.Api.Sharing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lorebound.Api.Controllers;

/// <summary>
/// Invites seen from the joining side, by code rather than by setting:
/// preview (P3-04) and accept (P3-05). Signed-in users only. Every unusable
/// code (unknown, malformed, expired, revoked or used up) gets the same 404,
/// so nobody can tell a dead code from one that never existed; the rate
/// limit slows down guessing.
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
    var userId = _currentUser.UserId;

    var preview = await UsableInvite(code)
        .AsNoTracking()
        .Select(invite => new InvitePreviewDto(
            invite.CampaignSetting.Name,
            invite.CampaignSetting.Owner.DisplayName,
            invite.CampaignSetting.OwnerUserId == userId
                || invite.CampaignSetting.Memberships.Any(m => m.UserId == userId)))
        .SingleOrDefaultAsync(cancellationToken);

    return preview ?? throw new NotFoundException(NotFoundMessage);
  }

  /// <summary>
  /// Joins the setting as a Player and uses up one use of the invite. A user
  /// who already belongs gets 200 with their current role and uses nothing,
  /// so retrying (a double click, a reload) is safe. Membership is checked
  /// before validity, so the retry of an accept that took the last use is
  /// still 200; a member learns nothing new from it. Everyone else gets the
  /// identical 404 for an unusable code.
  /// </summary>
  [HttpPost("{code}/accept")]
  [ProducesResponseType<AcceptInviteResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
  public async Task<ActionResult<AcceptInviteResponse>> Accept(
      string code,
      CancellationToken cancellationToken)
  {
    var userId = _currentUser.UserId;

    var found = await InviteByCode(code)
        .AsNoTracking()
        .Select(invite => new
        {
          Invite = invite,
          invite.Id,
          SettingId = invite.CampaignSettingId,
          IsOwner = invite.CampaignSetting.OwnerUserId == userId,
          Role = invite.CampaignSetting.Memberships
              .Where(m => m.UserId == userId)
              .Select(m => (SettingRole?)m.Role)
              .FirstOrDefault(),
        })
        .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(NotFoundMessage);

    if (found.IsOwner)
    {
      return Ok(new AcceptInviteResponse(found.SettingId, SettingRole.GameMaster));
    }

    if (found.Role is { } existingRole)
    {
      return Ok(new AcceptInviteResponse(found.SettingId, existingRole));
    }

    if (found.Invite.StatusAt(_timeProvider.GetUtcNow()) != InviteStatus.Active)
    {
      throw new NotFoundException(NotFoundMessage);
    }

    // Membership and use count change together or not at all. Two accepts
    // of a last use: Postgres makes the second UPDATE wait for the first to
    // commit, then re-checks UseCount < MaxUses, so it matches no row. Two
    // accepts by the same user: the second INSERT waits on the unique index,
    // then fails, and is answered as already-member.
    await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

    var membership = new SettingMembership
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = found.SettingId,
      UserId = userId,
      Role = SettingRole.Player,
    };
    _db.SettingMemberships.Add(membership);

    try
    {
      await _db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException error) when (IsDuplicateMembership(error))
    {
      await transaction.RollbackAsync(cancellationToken);
      _db.Entry(membership).State = EntityState.Detached;
      return Ok(new AcceptInviteResponse(found.SettingId, await CurrentRoleAsync(found.SettingId, cancellationToken)));
    }

    var consumed = await _db.SettingInvites
        .Where(invite => invite.Id == found.Id)
        .Where(InviteRules.IsActiveAt(_timeProvider.GetUtcNow()))
        .ExecuteUpdateAsync(
            setters => setters.SetProperty(invite => invite.UseCount, invite => invite.UseCount + 1),
            cancellationToken);

    if (consumed == 0)
    {
      // Used up, revoked or expired since the lookup above.
      await transaction.RollbackAsync(cancellationToken);
      throw new NotFoundException(NotFoundMessage);
    }

    await transaction.CommitAsync(cancellationToken);

    return Ok(new AcceptInviteResponse(found.SettingId, SettingRole.Player));
  }

  private IQueryable<SettingInvite> UsableInvite(string code) =>
      InviteByCode(code).Where(InviteRules.IsActiveAt(_timeProvider.GetUtcNow()));

  // A string that cannot be a code is 404 straight away, without a lookup.
  private IQueryable<SettingInvite> InviteByCode(string code) =>
      InviteCodes.TryNormalize(code, out var normalized)
          ? _db.SettingInvites.Where(invite => invite.Code == normalized)
          : throw new NotFoundException(NotFoundMessage);

  private Task<SettingRole> CurrentRoleAsync(Guid settingId, CancellationToken cancellationToken) =>
      _db.SettingMemberships
          .AsNoTracking()
          .Where(m => m.CampaignSettingId == settingId && m.UserId == _currentUser.UserId)
          .Select(m => m.Role)
          .SingleAsync(cancellationToken);

  private static bool IsDuplicateMembership(DbUpdateException error) =>
      error.InnerException is PostgresException
      {
        SqlState: PostgresErrorCodes.UniqueViolation,
        ConstraintName: "IX_SettingMemberships_CampaignSettingId_UserId",
      };
}
