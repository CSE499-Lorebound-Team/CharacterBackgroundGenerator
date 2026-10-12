using System.ComponentModel.DataAnnotations;
using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Characters;

/// <summary>
/// Replaces a character's name and backstory (P6-06). The name is trimmed
/// and must not be blank. The backstory is plain free text, stored as sent;
/// blank or missing clears it.
/// </summary>
public record UpdateCharacterRequest(
    [Required]
    [StringLength(Character.NameMaxLength, MinimumLength = 1)]
    string Name,

    [StringLength(Character.BackstoryMaxLength)]
    string? Backstory);
