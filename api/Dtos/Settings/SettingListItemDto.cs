namespace Lorebound.Api.Dtos.Settings;

public record SettingListItemDto(
    Guid Id,
    string Name,
    string? Description,
    int EntryCount,
    string MyRole,
    bool IsOwner,
    string OwnerDisplayName,
    DateTimeOffset UpdatedAt);