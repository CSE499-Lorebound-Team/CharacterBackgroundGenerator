using System.ComponentModel.DataAnnotations;
using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Relationships;

/// <summary>
/// Replaces a relationship's type and description (P5-04). The endpoints
/// cannot change: <see cref="SourceEntryId"/> and <see cref="TargetEntryId"/>
/// are accepted only so that a different value can be rejected with 400
/// instead of being silently ignored. Delete and recreate to re-link.
/// </summary>
public record UpdateRelationshipRequest(
    [Required]
    [StringLength(RelationshipTypes.MaxLength, MinimumLength = 1)]
    string RelationshipType,

    [StringLength(1000)]
    string? Description,

    Guid? SourceEntryId = null,

    Guid? TargetEntryId = null);
