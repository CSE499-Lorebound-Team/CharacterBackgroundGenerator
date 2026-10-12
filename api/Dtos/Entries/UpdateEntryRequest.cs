using System.ComponentModel.DataAnnotations;
using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Entries;

/// <summary>Replaces every field; same rules as <see cref="CreateEntryRequest"/>.</summary>
public record UpdateEntryRequest(
    [Required]
    [StringLength(120, MinimumLength = 1)]
    string Name,

    [Required]
    [EnumDataType(typeof(SettingEntryType))]
    SettingEntryType? EntryType,

    [StringLength(4000)]
    string? Description,

    bool IsGmOnly = false);
