using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Settings;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lorebound.Api.Errors;
using Lorebound.Api.Dtos.Common;

namespace Lorebound.Api.Controllers;

/// <summary>
/// Campaign settings (Phase 2). Everyone sees only the settings they own or
/// belong to; any other id is 404, like a missing one.
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
private readonly LoreboundDbContext _db;
private readonly ICurrentUser _currentUser;
private readonly ISettingAccess _settingAccess;

  public SettingsController(
      LoreboundDbContext db,
      ICurrentUser currentUser,
      ISettingAccess settingAccess)
  {
    _db = db;
    _currentUser = currentUser;
    _settingAccess = settingAccess;
  }

  /// <summary>
  /// Creates a setting owned by the caller, who becomes its GameMaster. The
  /// name is trimmed and must be unique among the caller's own settings,
  /// ignoring case (409 otherwise).
  /// </summary>
  [HttpPost]
[ProducesResponseType<SettingDetailDto>(
    StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<ActionResult<SettingDetailDto>> Create(
    CreateSettingRequest request)
    {
    var name = request.Name.Trim();

    var duplicateExists =
        await _db.CampaignSettings
            .AsNoTracking()
            .AnyAsync(setting =>
                setting.OwnerUserId == _currentUser.UserId &&
                EF.Functions.ILike(
                setting.Name,
                EscapeLikePattern(name),
                "\\"));

    if (duplicateExists)
    {
        throw new ConflictException(
            "A setting with this name already exists.");
    }

    var setting = new CampaignSetting
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description =
            NormalizeDescription(
                request.Description),
        OwnerUserId =
            _currentUser.UserId,
    };

    var membership = new SettingMembership
    {
        Id = Guid.NewGuid(),
        CampaignSettingId = setting.Id,
        UserId = _currentUser.UserId,
        Role = SettingRole.GameMaster,
        JoinedAt = DateTimeOffset.UtcNow,
    };

    _db.CampaignSettings.Add(setting);
    _db.SettingMemberships.Add(membership);

    await _db.SaveChangesAsync();

    var dto = ToDetailDto(
    setting,
    "GameMaster",
    true,
    _currentUser.DisplayName,
    1,
    new Dictionary<SettingEntryType, int>());

    return CreatedAtAction(
        nameof(GetById),
        new { id = setting.Id },
        dto);
    }

  /// <summary>
  /// The settings the caller owns or belongs to, most recently updated
  /// first. <paramref name="search"/> matches the name (contains, ignoring
  /// case); <paramref name="role"/> is <c>gm</c> or <c>player</c> (400
  /// otherwise). Players' entry counts leave out GM-only entries.
  /// </summary>
  [HttpGet]
[ProducesResponseType<PagedResult<SettingListItemDto>>(
    StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<PagedResult<SettingListItemDto>>> GetAll(
    [FromQuery] string? search,
    [FromQuery] string? role,
    [FromQuery] PageQuery pageQuery)
{
  var userId = _currentUser.UserId;

  var query =
      _settingAccess
          .VisibleToCurrentUser()
          .AsNoTracking();

  if (!string.IsNullOrWhiteSpace(search))
  {
    var pattern =
        $"%{EscapeLikePattern(search.Trim())}%";

    query = query.Where(setting =>
        EF.Functions.ILike(
            setting.Name,
            pattern,
            "\\"));
  }

  if (!string.IsNullOrWhiteSpace(role))
  {
    var normalizedRole =
        role.Trim().ToLowerInvariant();

    if (normalizedRole == "gm")
    {
      query = query.Where(setting =>
          setting.OwnerUserId == userId ||
          setting.Memberships.Any(membership =>
              membership.UserId == userId &&
              membership.Role == SettingRole.GameMaster));
    }
    else if (normalizedRole == "player")
    {
      query = query.Where(setting =>
          setting.OwnerUserId != userId &&
          setting.Memberships.Any(membership =>
              membership.UserId == userId &&
              membership.Role == SettingRole.Player));
    }
    else
    {
      return BadRequest(
          new ProblemDetails
          {
            Title = "Invalid role filter.",
            Detail = "Role must be 'gm' or 'player'.",
            Status = StatusCodes.Status400BadRequest,
          });
    }
  }

  var totalCount =
      await query.CountAsync();

  var items =
      await query
          .OrderByDescending(setting =>
              setting.UpdatedAt)
          .Skip(pageQuery.Skip)
          .Take(pageQuery.PageSize)
          .ToListItems(userId)
          .ToListAsync();

  return Ok(
      new PagedResult<SettingListItemDto>(
          items,
          pageQuery.Page,
          pageQuery.PageSize,
          totalCount));
}

  /// <summary>
  /// One setting with the caller's role, the member count and entry counts
  /// by type (GM-only entries left out for Players). Any member may read
  /// it; anyone else gets 404.
  /// </summary>
  [HttpGet("{id:guid}")]
[ProducesResponseType<SettingDetailDto>(
    StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<SettingDetailDto>> GetById(
    Guid id)
{
  var setting =
      await _settingAccess.RequireMemberAsync(id);

  var role =
      setting.OwnerUserId == _currentUser.UserId
          ? SettingRole.GameMaster
          : await _settingAccess.GetRoleAsync(id);

  var ownerDisplayName =
      await _db.Users
          .AsNoTracking()
          .Where(user =>
              user.Id == setting.OwnerUserId)
          .Select(user =>
              user.DisplayName)
          .SingleAsync();

  var memberCount =
      await _db.SettingMemberships
          .CountAsync(membership =>
              membership.CampaignSettingId == id);

  var entryCounts =
      await _db.SettingEntries
          .AsNoTracking()
          .Where(entry =>
              entry.CampaignSettingId == id)
          .VisibleTo(role ?? SettingRole.Player)
          .GroupBy(entry =>
              entry.EntryType)
          .Select(group =>
              new
              {
                Type = group.Key,
                Count = group.Count(),
              })
          .ToDictionaryAsync(
              item => item.Type,
              item => item.Count);

  return Ok(
      new SettingDetailDto(
          setting.Id,
          setting.Name,
          setting.Description,
          role?.ToString() ?? "GameMaster",
          setting.OwnerUserId ==
              _currentUser.UserId,
          ownerDisplayName,
          memberCount,
          entryCounts,
          setting.CreatedAt,
          setting.UpdatedAt));
}

  /// <summary>
  /// Renames the setting and replaces its description. GameMasters only
  /// (Players get 403); the name must stay unique among the owner's
  /// settings (409).
  /// </summary>
  [HttpPut("{id:guid}")]
[ProducesResponseType<SettingDetailDto>(
    StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<ActionResult<SettingDetailDto>> Update(
    Guid id,
    UpdateSettingRequest request)
{
  var setting =
      await _settingAccess.RequireGameMasterAsync(id);

  var name = request.Name.Trim();

  var duplicateExists =
      await _db.CampaignSettings
          .AsNoTracking()
          .AnyAsync(otherSetting =>
              otherSetting.Id != id &&
              otherSetting.OwnerUserId == setting.OwnerUserId &&
              EF.Functions.ILike(
                otherSetting.Name,
                EscapeLikePattern(name),
                "\\"));

  if (duplicateExists)
  {
    throw new ConflictException(
        "A setting with this name already exists.");
  }

  setting.Name = name;

  setting.Description =
      NormalizeDescription(
          request.Description);

  await _db.SaveChangesAsync();

  var role =
      setting.OwnerUserId == _currentUser.UserId
          ? SettingRole.GameMaster
          : await _settingAccess.GetRoleAsync(id);

  var ownerDisplayName =
      await _db.Users
          .AsNoTracking()
          .Where(user =>
              user.Id == setting.OwnerUserId)
          .Select(user =>
              user.DisplayName)
          .SingleAsync();

  var memberCount =
      await _db.SettingMemberships
          .CountAsync(membership =>
              membership.CampaignSettingId == id);

  var entryCounts =
      await _db.SettingEntries
          .AsNoTracking()
          .Where(entry =>
              entry.CampaignSettingId == id)
          .VisibleTo(role ?? SettingRole.Player)
          .GroupBy(entry =>
              entry.EntryType)
          .Select(group =>
              new
              {
                Type = group.Key,
                Count = group.Count(),
              })
          .ToDictionaryAsync(
              item => item.Type,
              item => item.Count);

  return Ok(
      ToDetailDto(
          setting,
          role?.ToString() ?? "GameMaster",
          setting.OwnerUserId ==
              _currentUser.UserId,
          ownerDisplayName,
          memberCount,
          entryCounts));
}

  /// <summary>
  /// Deletes the setting with its entries, relationships, memberships,
  /// invites and characters. The owner only; other members get 403.
  /// </summary>
  [HttpDelete("{id:guid}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Delete(
    Guid id)
{
  var setting =
    await _settingAccess.RequireOwnerAsync(id);

    var relationships =
        await _db.SettingEntryRelationships
            .Where(relationship =>
                relationship.CampaignSettingId == id)
            .ToListAsync();

    _db.SettingEntryRelationships.RemoveRange(
        relationships);

    _db.CampaignSettings.Remove(setting);

    await _db.SaveChangesAsync();

    return NoContent();
}

private static SettingDetailDto ToDetailDto(
    CampaignSetting setting,
    string myRole,
    bool isOwner,
    string ownerDisplayName,
    int memberCount,
    IReadOnlyDictionary<SettingEntryType, int> entryCountsByType)
{
  return new SettingDetailDto(
      setting.Id,
      setting.Name,
      setting.Description,
      myRole,
      isOwner,
      ownerDisplayName,
      memberCount,
      entryCountsByType,
      setting.CreatedAt,
      setting.UpdatedAt);
}

private static string EscapeLikePattern(
    string value)
{
  return value
      .Replace("\\", "\\\\")
      .Replace("%", "\\%")
      .Replace("_", "\\_");
}
  private static string? NormalizeDescription(
      string? description)
  {
    if (string.IsNullOrWhiteSpace(
        description))
    {
      return null;
    }

    return description.Trim();
  }
}