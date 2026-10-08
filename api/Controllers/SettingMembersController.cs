using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Common;
using Lorebound.Api.Dtos.Members;
using Lorebound.Api.Errors;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Controllers;

/// <summary>
/// Who belongs to a setting (P3-06). Any member can see the list; only the
/// owner changes roles, so there are no co-owners. Other GameMasters can
/// still manage invites and remove Players (P3-07).
/// </summary>
[ApiController]
[Route("api/settings/{settingId:guid}/members")]
public class SettingMembersController : ControllerBase
{
  private readonly LoreboundDbContext _db;
  private readonly ISettingAccess _settingAccess;

  public SettingMembersController(
      LoreboundDbContext db,
      ISettingAccess settingAccess)
  {
    _db = db;
    _settingAccess = settingAccess;
  }

  /// <summary>Owner first, then GameMasters, then Players, each oldest first.</summary>
  [HttpGet]
  [ProducesResponseType<PagedResult<MemberDto>>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PagedResult<MemberDto>>> List(
      Guid settingId,
      [FromQuery] PageQuery pageQuery,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireMemberAsync(settingId, cancellationToken);

    var query = _db.SettingMemberships
        .AsNoTracking()
        .Where(m => m.CampaignSettingId == settingId);

    var totalCount = await query.CountAsync(cancellationToken);

    var items = await query
        .OrderByDescending(m => m.UserId == m.CampaignSetting.OwnerUserId)
        .ThenBy(m => m.Role == SettingRole.GameMaster ? 0 : 1)
        .ThenBy(m => m.JoinedAt)
        .ThenBy(m => m.UserId)
        .Skip(pageQuery.Skip)
        .Take(pageQuery.PageSize)
        .Select(m => new MemberDto(
            m.UserId,
            m.User.DisplayName,
            m.Role,
            m.UserId == m.CampaignSetting.OwnerUserId,
            m.JoinedAt))
        .ToListAsync(cancellationToken);

    return Ok(new PagedResult<MemberDto>(items, pageQuery.Page, pageQuery.PageSize, totalCount));
  }

  /// <summary>
  /// Promotes or demotes a member. Owner only. Setting the role a member
  /// already has is a no-op 200; the owner is always a GameMaster.
  /// </summary>
  [HttpPatch("{userId:guid}")]
  [ProducesResponseType<MemberDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<MemberDto>> UpdateRole(
      Guid settingId,
      Guid userId,
      UpdateMemberRoleRequest request,
      CancellationToken cancellationToken)
  {
    var setting = await _settingAccess.RequireOwnerAsync(settingId, cancellationToken);
    var role = request.Role!.Value;

    var membership = await _db.SettingMemberships
        .Include(m => m.User)
        .SingleOrDefaultAsync(
            m => m.CampaignSettingId == settingId && m.UserId == userId,
            cancellationToken)
        ?? throw new NotFoundException("Member not found.");

    var isOwner = userId == setting.OwnerUserId;

    if (membership.Role != role)
    {
      if (isOwner)
      {
        throw new ConflictException("The owner is always a GameMaster and cannot be demoted.");
      }

      membership.Role = role;
      await _db.SaveChangesAsync(cancellationToken);
    }

    return Ok(new MemberDto(
        membership.UserId,
        membership.User.DisplayName,
        membership.Role,
        isOwner,
        membership.JoinedAt));
  }
}
