using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Relationships;

/// <summary>
/// Links two entries of the setting in the route (P5-03). The setting is
/// always taken from the route, never from the body.
/// </summary>
public record CreateRelationshipRequest(
    [Required]
    Guid? SourceEntryId,

    [Required]
    Guid? TargetEntryId,

    [Required]
    [StringLength(60, MinimumLength = 1)]
    string RelationshipType,

    [StringLength(1000)]
    string? Description);
