using System.ComponentModel.DataAnnotations;
using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Entries;

public record CreateEntryRequest(
    [Required]
    [StringLength(120, MinimumLength = 1)]
    string Name,

    [Required]
    [EnumDataType(typeof(SettingEntryType))]
    SettingEntryType? EntryType,

    [StringLength(4000)]
    string? Description,

    bool IsGmOnly = false);
