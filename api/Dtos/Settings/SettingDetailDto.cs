using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Settings;

public record SettingDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string MyRole,
    bool IsOwner,
    string OwnerDisplayName,
    int MemberCount,
    IReadOnlyDictionary<SettingEntryType, int> EntryCountsByType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);