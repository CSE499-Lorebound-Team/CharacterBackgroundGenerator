using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Common;
using Lorebound.Api.Dtos.Entries;
using Lorebound.Api.Errors;
using Lorebound.Api.Mapping;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
  private const string UniqueNameIndex = "IX_SettingEntries_CampaignSettingId_EntryType_Name";

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

  /// <summary>
  /// One entry with its outgoing and incoming relationships, each sorted by
  /// the other entry's name. A Player asking for a GM-only entry gets the
  /// same 404 as for a missing one, and relationships to GM-only entries are
  /// left out, so a Player cannot learn a hidden entry exists.
  /// </summary>
  [HttpGet("{entryId:guid}")]
  [ProducesResponseType<EntryDetailDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<EntryDetailDto>> Get(
      Guid settingId,
      Guid entryId,
      CancellationToken cancellationToken)
  {
    var role = await _settingAccess.RequireMemberRoleAsync(settingId, cancellationToken);
    var isGameMaster = role == SettingRole.GameMaster;

    var detail = await _db.SettingEntries
        .AsNoTracking()
        .Where(entry => entry.Id == entryId && entry.CampaignSettingId == settingId)
        .VisibleTo(role)
        .Select(entry => new EntryDetailDto(
            entry.Id,
            entry.CampaignSettingId,
            entry.Name,
            entry.EntryType,
            entry.Description,
            isGameMaster ? entry.IsGmOnly : null,
            entry.OutgoingRelationships
                .Where(r => isGameMaster || !r.TargetEntry.IsGmOnly)
                .OrderBy(r => r.TargetEntry.Name)
                .ThenBy(r => r.Id)
                .Select(r => new EntryRelationshipDto(
                    r.Id,
                    r.TargetEntryId,
                    r.TargetEntry.Name,
                    r.TargetEntry.EntryType,
                    r.RelationshipType,
                    r.Description))
                .ToList(),
            entry.IncomingRelationships
                .Where(r => isGameMaster || !r.SourceEntry.IsGmOnly)
                .OrderBy(r => r.SourceEntry.Name)
                .ThenBy(r => r.Id)
                .Select(r => new EntryRelationshipDto(
                    r.Id,
                    r.SourceEntryId,
                    r.SourceEntry.Name,
                    r.SourceEntry.EntryType,
                    r.RelationshipType,
                    r.Description))
                .ToList(),
            entry.CreatedAt,
            entry.UpdatedAt))
        .AsSplitQuery()
        .SingleOrDefaultAsync(cancellationToken);

    return Ok(detail ?? throw EntryNotFound());
  }

  /// <summary>
  /// Adds an entry. GameMasters only. The name is trimmed and must be unique
  /// per type within the setting, ignoring case (409 otherwise).
  /// </summary>
  [HttpPost]
  [ProducesResponseType<SettingEntryDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<SettingEntryDto>> Create(
      Guid settingId,
      CreateEntryRequest request,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireGameMasterAsync(settingId, cancellationToken);

    var entry = new SettingEntry
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = settingId,
      Name = request.Name.Trim(),
      EntryType = request.EntryType!.Value,
      Description = NormalizeDescription(request.Description),
      IsGmOnly = request.IsGmOnly,
    };

    _db.SettingEntries.Add(entry);
    await SaveUniqueAsync(entry, cancellationToken);

    return Created(
        $"/api/settings/{settingId}/entries/{entry.Id}",
        entry.ToDto(SettingRole.GameMaster));
  }

  /// <summary>
  /// Saves <paramref name="entry"/>, answering 409 when another entry of the
  /// same setting and type already has its name (ignoring case). Checked up
  /// front for the common case; the unique index catches a concurrent save.
  /// </summary>
  private async Task SaveUniqueAsync(
      SettingEntry entry,
      CancellationToken cancellationToken)
  {
    // Name is citext, so this comparison ignores case.
    var taken = await _db.SettingEntries.AnyAsync(
        other => other.CampaignSettingId == entry.CampaignSettingId
            && other.EntryType == entry.EntryType
            && other.Name == entry.Name
            && other.Id != entry.Id,
        cancellationToken);

    if (taken)
    {
      throw DuplicateName(entry.EntryType);
    }

    try
    {
      await _db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException error) when (error.InnerException is PostgresException
    {
      SqlState: PostgresErrorCodes.UniqueViolation,
      ConstraintName: UniqueNameIndex,
    })
    {
      throw DuplicateName(entry.EntryType);
    }
  }

  // One message for missing, hidden and other-setting entries alike.
  private static NotFoundException EntryNotFound() => new("Entry not found.");

  private static ConflictException DuplicateName(SettingEntryType entryType) =>
      new($"A {entryType} with this name already exists in this setting.");

  private static string? NormalizeDescription(string? description) =>
      string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
