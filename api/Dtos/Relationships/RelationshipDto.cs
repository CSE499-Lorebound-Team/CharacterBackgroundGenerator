using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Relationships;

/// <summary>
/// A link from <see cref="Source"/> to <see cref="Target"/> (P5-02, P5-03).
/// </summary>
public record RelationshipDto(
    Guid Id,
    RelationshipEndpointDto Source,
    RelationshipEndpointDto Target,
    string RelationshipType,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>The entry at one end of a relationship.</summary>
public record RelationshipEndpointDto(
    Guid Id,
    string Name,
    SettingEntryType EntryType);
