using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Common;
using Lorebound.Api.Dtos.Relationships;
using Lorebound.Api.Errors;
using Lorebound.Api.Mapping;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lorebound.Api.Controllers;

/// <summary>
/// Typed links between a setting's entries (Phase 5). Any member reads;
/// only GameMasters write. A Player never sees a link where either end is
/// GM-only: every read goes through
/// <see cref="SettingEntryQueries.VisibleTo(IQueryable{SettingEntryRelationship}, SettingRole)"/>.
/// </summary>
[ApiController]
[Route("api/settings/{settingId:guid}/relationships")]
public class SettingRelationshipsController : ControllerBase
{
  private const string UniqueLinkIndex = "IX_SettingEntryRelationships_Source_Target_Type";

  private readonly LoreboundDbContext _db;
  private readonly ISettingAccess _settingAccess;

  public SettingRelationshipsController(
      LoreboundDbContext db,
      ISettingAccess settingAccess)
  {
    _db = db;
    _settingAccess = settingAccess;
  }

  /// <summary>
  /// The setting's relationships, sorted by source name, type and target
  /// name. <paramref name="entryId"/> keeps the links of one entry in either
  /// direction (404 if the caller cannot see that entry);
  /// <paramref name="type"/> keeps one relationship type, ignoring case.
  /// </summary>
  [HttpGet]
  [ProducesResponseType<PagedResult<RelationshipDto>>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PagedResult<RelationshipDto>>> List(
      Guid settingId,
      [FromQuery] Guid? entryId,
      [FromQuery] string? type,
      [FromQuery] PageQuery pageQuery,
      CancellationToken cancellationToken)
  {
    var role = await _settingAccess.RequireMemberRoleAsync(settingId, cancellationToken);

    var query = _db.SettingEntryRelationships
        .AsNoTracking()
        .Where(r => r.CampaignSettingId == settingId)
        .VisibleTo(role);

    if (entryId is { } id)
    {
      // Same 404 for a missing, hidden or other-setting entry, as on the
      // entry detail, so a Player cannot probe for hidden entries.
      var entryVisible = await _db.SettingEntries
          .Where(entry => entry.Id == id && entry.CampaignSettingId == settingId)
          .VisibleTo(role)
          .AnyAsync(cancellationToken);

      if (!entryVisible)
      {
        throw new NotFoundException("Entry not found.");
      }

      query = query.Where(r => r.SourceEntryId == id || r.TargetEntryId == id);
    }

    if (!string.IsNullOrWhiteSpace(type))
    {
      // RelationshipType is citext, so this equality ignores case.
      var relationshipType = RelationshipTypes.Normalize(type);

      query = query.Where(r => r.RelationshipType == relationshipType);
    }

    var totalCount = await query.CountAsync(cancellationToken);

    var items = await query
        .OrderBy(r => r.SourceEntry.Name)
        .ThenBy(r => r.RelationshipType)
        .ThenBy(r => r.TargetEntry.Name)
        .ThenBy(r => r.Id)
        .Skip(pageQuery.Skip)
        .Take(pageQuery.PageSize)
        .Select(r => new RelationshipDto(
            r.Id,
            new RelationshipEndpointDto(r.SourceEntryId, r.SourceEntry.Name, r.SourceEntry.EntryType),
            new RelationshipEndpointDto(r.TargetEntryId, r.TargetEntry.Name, r.TargetEntry.EntryType),
            r.RelationshipType,
            r.Description,
            r.CreatedAt,
            r.UpdatedAt))
        .ToListAsync(cancellationToken);

    return Ok(new PagedResult<RelationshipDto>(items, pageQuery.Page, pageQuery.PageSize, totalCount));
  }

  /// <summary>
  /// Links two entries. GameMasters only. Both entries must belong to the
  /// setting in the route (400 otherwise, the same for a missing entry); a
  /// self-link is 400 and an existing (source, target, type) link is 409.
  /// </summary>
  [HttpPost]
  [ProducesResponseType<RelationshipDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<RelationshipDto>> Create(
      Guid settingId,
      CreateRelationshipRequest request,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireGameMasterAsync(settingId, cancellationToken);

    var sourceId = request.SourceEntryId!.Value;
    var targetId = request.TargetEntryId!.Value;

    if (sourceId == targetId)
    {
      ModelState.AddModelError(nameof(request.TargetEntryId), "An entry cannot be linked to itself.");
      return ValidationProblem(ModelState);
    }

    // Scoped to the route's setting: an entry of another setting is
    // indistinguishable from a missing one.
    var entries = await _db.SettingEntries
        .Where(entry => entry.CampaignSettingId == settingId
            && (entry.Id == sourceId || entry.Id == targetId))
        .ToDictionaryAsync(entry => entry.Id, cancellationToken);

    foreach (var (field, id) in new[]
    {
      (nameof(request.SourceEntryId), sourceId),
      (nameof(request.TargetEntryId), targetId),
    })
    {
      if (!entries.ContainsKey(id))
      {
        ModelState.AddModelError(field, "Entry not found in this setting.");
      }
    }

    if (!ModelState.IsValid)
    {
      return ValidationProblem(ModelState);
    }

    var relationship = new SettingEntryRelationship
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = settingId,
      SourceEntryId = sourceId,
      TargetEntryId = targetId,
      RelationshipType = RelationshipTypes.Normalize(request.RelationshipType),
      Description = NormalizeDescription(request.Description),
    };

    // The entries are tracked, so EF fills in SourceEntry and TargetEntry.
    _db.SettingEntryRelationships.Add(relationship);
    await SaveUniqueAsync(relationship, cancellationToken);

    return Created(
        $"/api/settings/{settingId}/relationships/{relationship.Id}",
        relationship.ToDto());
  }

  /// <summary>
  /// Replaces a relationship's type and description. GameMasters only. The
  /// endpoints are immutable: a <c>sourceEntryId</c> or <c>targetEntryId</c>
  /// that differs from the current one is 400 (delete and recreate to
  /// re-link). Changing to a type the same link already has is 409.
  /// </summary>
  [HttpPut("{relationshipId:guid}")]
  [ProducesResponseType<RelationshipDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<RelationshipDto>> Update(
      Guid settingId,
      Guid relationshipId,
      UpdateRelationshipRequest request,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireGameMasterAsync(settingId, cancellationToken);

    var relationship = await FindRelationshipAsync(settingId, relationshipId, cancellationToken);

    foreach (var (field, requested, current) in new[]
    {
      (nameof(request.SourceEntryId), request.SourceEntryId, relationship.SourceEntryId),
      (nameof(request.TargetEntryId), request.TargetEntryId, relationship.TargetEntryId),
    })
    {
      if (requested is { } id && id != current)
      {
        ModelState.AddModelError(field,
            "A relationship's entries cannot change. Delete it and create a new one.");
      }
    }

    if (!ModelState.IsValid)
    {
      return ValidationProblem(ModelState);
    }

    relationship.RelationshipType = RelationshipTypes.Normalize(request.RelationshipType);
    relationship.Description = NormalizeDescription(request.Description);

    await SaveUniqueAsync(relationship, cancellationToken);

    return Ok(relationship.ToDto());
  }

  /// <summary>Deletes a relationship. GameMasters only; the entries stay.</summary>
  [HttpDelete("{relationshipId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
      Guid settingId,
      Guid relationshipId,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireGameMasterAsync(settingId, cancellationToken);

    var deleted = await _db.SettingEntryRelationships
        .Where(r => r.Id == relationshipId && r.CampaignSettingId == settingId)
        .ExecuteDeleteAsync(cancellationToken);

    return deleted == 0 ? throw RelationshipNotFound() : NoContent();
  }

  // Tracked, with both entries, scoped to the setting so an id from another
  // setting is 404.
  private async Task<SettingEntryRelationship> FindRelationshipAsync(
      Guid settingId,
      Guid relationshipId,
      CancellationToken cancellationToken) =>
      await _db.SettingEntryRelationships
          .Include(r => r.SourceEntry)
          .Include(r => r.TargetEntry)
          .SingleOrDefaultAsync(
              r => r.Id == relationshipId && r.CampaignSettingId == settingId,
              cancellationToken)
      ?? throw RelationshipNotFound();

  /// <summary>
  /// Saves <paramref name="relationship"/>, answering 409 when the same
  /// source, target and type (ignoring case) already exist. Checked up front
  /// for the common case; the unique index catches a concurrent save.
  /// </summary>
  private async Task SaveUniqueAsync(
      SettingEntryRelationship relationship,
      CancellationToken cancellationToken)
  {
    // RelationshipType is citext, so this comparison ignores case.
    var exists = await _db.SettingEntryRelationships.AnyAsync(
        r => r.SourceEntryId == relationship.SourceEntryId
            && r.TargetEntryId == relationship.TargetEntryId
            && r.RelationshipType == relationship.RelationshipType
            && r.Id != relationship.Id,
        cancellationToken);

    if (exists)
    {
      throw DuplicateLink();
    }

    try
    {
      await _db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException error) when (error.InnerException is PostgresException
    {
      SqlState: PostgresErrorCodes.UniqueViolation,
      ConstraintName: UniqueLinkIndex,
    })
    {
      throw DuplicateLink();
    }
    catch (DbUpdateException error) when (error.InnerException is PostgresException
    {
      SqlState: PostgresErrorCodes.ForeignKeyViolation,
    })
    {
      // An entry was deleted after it was looked up.
      throw new ConflictException("One of the entries was deleted. Try again.");
    }
  }

  private static NotFoundException RelationshipNotFound() => new("Relationship not found.");

  private static ConflictException DuplicateLink() =>
      new("These entries are already linked with this relationship type.");

  private static string? NormalizeDescription(string? description) =>
      string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
