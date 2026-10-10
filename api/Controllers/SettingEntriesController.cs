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
/// goes through <see cref="SettingEntryQueries.VisibleTo(IQueryable{SettingEntry}, SettingRole)"/>, and links to a
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
  /// Replaces an entry's name, type, description and secrecy. GameMasters
  /// only; same rules as create, and the name check ignores the entry
  /// itself. Making an entry GM-only does not change characters that already
  /// chose it (decision recorded in P6-05).
  /// </summary>
  [HttpPut("{entryId:guid}")]
  [ProducesResponseType<SettingEntryDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<SettingEntryDto>> Update(
      Guid settingId,
      Guid entryId,
      UpdateEntryRequest request,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireGameMasterAsync(settingId, cancellationToken);

    var entry = await FindEntryAsync(settingId, entryId, cancellationToken);

    entry.Name = request.Name.Trim();
    entry.EntryType = request.EntryType!.Value;
    entry.Description = NormalizeDescription(request.Description);
    entry.IsGmOnly = request.IsGmOnly;

    await SaveUniqueAsync(entry, cancellationToken);

    return Ok(entry.ToDto(SettingRole.GameMaster));
  }

  /// <summary>
  /// Deletes an entry. GameMasters only. An entry that still has
  /// relationships (either direction) or that characters have chosen is 409
  /// with <c>relationshipCount</c> and <c>characterCount</c> and nothing
  /// changes, unless <paramref name="force"/> is true: then its
  /// relationships and the entry go together in one transaction, and the
  /// characters keep their choices without the entry (P6-09).
  /// </summary>
  [HttpDelete("{entryId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> Delete(
      Guid settingId,
      Guid entryId,
      [FromQuery] bool force,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireGameMasterAsync(settingId, cancellationToken);

    var entry = await FindEntryAsync(settingId, entryId, cancellationToken);

    var relationships = _db.SettingEntryRelationships
        .Where(r => r.SourceEntryId == entryId || r.TargetEntryId == entryId);

    var relationshipCount = await relationships.CountAsync(cancellationToken);

    // Characters (not choices) that chose this entry, whatever their owner.
    var characterCount = await _db.CharacterChoices
        .Where(choice => choice.EntryId == entryId)
        .Select(choice => choice.CharacterId)
        .Distinct()
        .CountAsync(cancellationToken);

    if ((relationshipCount > 0 || characterCount > 0) && !force)
    {
      throw new ConflictException(
          $"This entry has {relationshipCount} relationship(s) and is chosen by " +
          $"{characterCount} character(s). Pass force=true to delete its relationships " +
          "with it; those characters keep their choice without the entry.",
          new Dictionary<string, object?>
          {
            ["relationshipCount"] = relationshipCount,
            ["characterCount"] = characterCount,
          });
    }

    // With force, the database sets CharacterChoice.EntryId to null (FK
    // SetNull, P6-01): the characters stay and show the choice with no
    // entry name (P6-05).
    await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

    await relationships.ExecuteDeleteAsync(cancellationToken);
    _db.SettingEntries.Remove(entry);

    try
    {
      await _db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException error) when (error.InnerException is PostgresException
    {
      SqlState: PostgresErrorCodes.ForeignKeyViolation,
    })
    {
      // A relationship was added between the count and the delete; the
      // transaction rolls back, so nothing changed.
      throw new ConflictException(
          "This entry's relationships changed while it was being deleted. Try again.");
    }

    await transaction.CommitAsync(cancellationToken);

    return NoContent();
  }

  // Tracked, scoped to the setting so an id from another setting is 404.
  private async Task<SettingEntry> FindEntryAsync(
      Guid settingId,
      Guid entryId,
      CancellationToken cancellationToken) =>
      await _db.SettingEntries.SingleOrDefaultAsync(
          entry => entry.Id == entryId && entry.CampaignSettingId == settingId,
          cancellationToken)
      ?? throw EntryNotFound();

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
