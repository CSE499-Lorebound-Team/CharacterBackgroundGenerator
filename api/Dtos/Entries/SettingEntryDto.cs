using System.Text.Json.Serialization;
using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Entries;

/// <summary>
/// One entry, returned by create and update (P4-03, P4-05).
/// <see cref="IsGmOnly"/> is only sent to GameMasters.
/// </summary>
public record SettingEntryDto(
    Guid Id,
    Guid CampaignSettingId,
    string Name,
    string? Description,
    SettingEntryType EntryType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? IsGmOnly,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
