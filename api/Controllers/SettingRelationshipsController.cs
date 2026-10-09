using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Common;
using Lorebound.Api.Dtos.Relationships;
using Lorebound.Api.Errors;
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
      // No wildcards in the pattern, so this is a case-insensitive equality.
      var pattern = LikePatterns.Escape(type.Trim());

      query = query.Where(r =>
          EF.Functions.ILike(r.RelationshipType, pattern, LikePatterns.EscapeCharacter));
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
      RelationshipType = request.RelationshipType.Trim(),
      Description = NormalizeDescription(request.Description),
    };

    var exists = await _db.SettingEntryRelationships.AnyAsync(
        r => r.SourceEntryId == sourceId
            && r.TargetEntryId == targetId
            && r.RelationshipType == relationship.RelationshipType,
        cancellationToken);

    if (exists)
    {
      throw DuplicateLink();
    }

    _db.SettingEntryRelationships.Add(relationship);

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
      // A concurrent request created the same link after the check above.
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

    var source = entries[sourceId];
    var target = entries[targetId];

    return Created(
        $"/api/settings/{settingId}/relationships/{relationship.Id}",
        new RelationshipDto(
            relationship.Id,
            new RelationshipEndpointDto(source.Id, source.Name, source.EntryType),
            new RelationshipEndpointDto(target.Id, target.Name, target.EntryType),
            relationship.RelationshipType,
            relationship.Description,
            relationship.CreatedAt,
            relationship.UpdatedAt));
  }

  private static ConflictException DuplicateLink() =>
      new("These entries are already linked with this relationship type.");

  private static string? NormalizeDescription(string? description) =>
      string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
