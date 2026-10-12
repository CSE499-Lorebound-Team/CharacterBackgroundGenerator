using System.Text.Json.Serialization;
using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Entries;

/// <summary>
/// One entry with its relationships (P4-04). Relationships whose other entry
/// the caller cannot see are left out. <see cref="IsGmOnly"/> is only sent
/// to GameMasters.
/// </summary>
public record EntryDetailDto(
    Guid Id,
    Guid CampaignSettingId,
    string Name,
    SettingEntryType EntryType,
    string? Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? IsGmOnly,
    IReadOnlyList<EntryRelationshipDto> Outgoing,
    IReadOnlyList<EntryRelationshipDto> Incoming,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// A relationship seen from one entry: the entry at the other end (the
/// target for outgoing, the source for incoming) and how they relate.
/// </summary>
public record EntryRelationshipDto(
    Guid Id,
    Guid OtherEntryId,
    string OtherEntryName,
    SettingEntryType OtherEntryType,
    string RelationshipType,
    string? Description);
