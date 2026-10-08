using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Common;
using Lorebound.Api.Dtos.Entries;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Controllers;

/// <summary>
/// A setting's lore entries (Phase 4). Any member reads; only GameMasters
/// write. GM-only entries are invisible to Players everywhere: every read
/// goes through <see cref="SettingEntryQueries.VisibleTo"/>, and links to a
/// hidden entry are left out of a Player's relationship lists and counts.
/// </summary>
[ApiController]
[Route("api/settings/{settingId:guid}/entries")]
public class SettingEntriesController : ControllerBase
{
  private readonly LoreboundDbContext _db;
  private readonly ISettingAccess _settingAccess;

  public SettingEntriesController(
      LoreboundDbContext db,
      ISettingAccess settingAccess)
  {
    _db = db;
    _settingAccess = settingAccess;
  }

  /// <summary>
  /// Entries of the setting by name, filtered by <paramref name="type"/>,
  /// <paramref name="search"/> (name or description, case-insensitive) and
  /// <paramref name="gmOnly"/>. For a Player, <c>gmOnly=true</c> matches
  /// nothing, since they never see GM-only entries.
  /// </summary>
  [HttpGet]
  [ProducesResponseType<PagedResult<EntryListItemDto>>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PagedResult<EntryListItemDto>>> List(
      Guid settingId,
      [FromQuery] SettingEntryType? type,
      [FromQuery] string? search,
      [FromQuery] bool? gmOnly,
      [FromQuery] PageQuery pageQuery,
      CancellationToken cancellationToken)
  {
    var role = await _settingAccess.RequireMemberRoleAsync(settingId, cancellationToken);
    var isGameMaster = role == SettingRole.GameMaster;

    var query = _db.SettingEntries
        .AsNoTracking()
        .Where(entry => entry.CampaignSettingId == settingId)
        .VisibleTo(role);

    if (type is { } entryType)
    {
      query = query.Where(entry => entry.EntryType == entryType);
    }

    if (gmOnly is { } isGmOnly)
    {
      query = query.Where(entry => entry.IsGmOnly == isGmOnly);
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
      var pattern = LikePatterns.Contains(search.Trim());

      query = query.Where(entry =>
          EF.Functions.ILike(entry.Name, pattern, LikePatterns.EscapeCharacter) ||
          (entry.Description != null &&
              EF.Functions.ILike(entry.Description, pattern, LikePatterns.EscapeCharacter)));
    }

    var totalCount = await query.CountAsync(cancellationToken);

    // A relationship counts only when the entry at its other end is visible
    // too, so a Player cannot infer hidden entries from the numbers.
    var items = await query
        .OrderBy(entry => entry.Name)
        .ThenBy(entry => entry.Id)
        .Skip(pageQuery.Skip)
        .Take(pageQuery.PageSize)
        .Select(entry => new EntryListItemDto(
            entry.Id,
            entry.Name,
            entry.EntryType,
            entry.Description,
            isGameMaster ? entry.IsGmOnly : null,
            entry.OutgoingRelationships.Count(r => isGameMaster || !r.TargetEntry.IsGmOnly) +
                entry.IncomingRelationships.Count(r => isGameMaster || !r.SourceEntry.IsGmOnly),
            entry.UpdatedAt))
        .ToListAsync(cancellationToken);

    return Ok(new PagedResult<EntryListItemDto>(items, pageQuery.Page, pageQuery.PageSize, totalCount));
  }
}
