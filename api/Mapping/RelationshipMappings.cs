using Lorebound.Api.Dtos.Relationships;
using Lorebound.Api.Models;

namespace Lorebound.Api.Mapping;

public static class RelationshipMappings
{
  /// <summary>
  /// Needs <see cref="SettingEntryRelationship.SourceEntry"/> and
  /// <see cref="SettingEntryRelationship.TargetEntry"/> loaded.
  /// </summary>
  public static RelationshipDto ToDto(this SettingEntryRelationship relationship) => new(
      relationship.Id,
      relationship.SourceEntry.ToEndpointDto(),
      relationship.TargetEntry.ToEndpointDto(),
      relationship.RelationshipType,
      relationship.Description,
      relationship.CreatedAt,
      relationship.UpdatedAt);

  private static RelationshipEndpointDto ToEndpointDto(this SettingEntry entry) =>
      new(entry.Id, entry.Name, entry.EntryType);
}
