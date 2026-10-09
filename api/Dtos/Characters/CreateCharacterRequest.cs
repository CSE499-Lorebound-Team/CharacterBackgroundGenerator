using System.ComponentModel.DataAnnotations;
using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Characters;

/// <summary>
/// Starts a draft character in a setting the caller belongs to (P6-03). A
/// missing or blank <see cref="Name"/> becomes "Unnamed Character".
/// </summary>
public record CreateCharacterRequest(
    [Required]
    Guid? SettingId,

    [StringLength(Character.NameMaxLength)]
    string? Name);
