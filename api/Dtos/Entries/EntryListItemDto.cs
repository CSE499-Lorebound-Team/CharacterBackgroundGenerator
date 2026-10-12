using System.Text.Json.Serialization;
using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Entries;

/// <summary>
/// One row of the entries list (P4-02). <see cref="IsGmOnly"/> is only sent
/// to GameMasters. <see cref="RelationshipCount"/> counts links in both
/// directions whose other entry the caller can see.
/// </summary>
public record EntryListItemDto(
    Guid Id,
    string Name,
    SettingEntryType EntryType,
    string? Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? IsGmOnly,
    int RelationshipCount,
    DateTimeOffset UpdatedAt);
