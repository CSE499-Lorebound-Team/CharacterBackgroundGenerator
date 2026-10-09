namespace Lorebound.Api.Dtos.Relationships;

/// <summary>
/// Relationship types to offer when linking entries (P5-05). Any other
/// text is accepted too.
/// </summary>
public record RelationshipTypesDto(IReadOnlyList<string> Suggested);
