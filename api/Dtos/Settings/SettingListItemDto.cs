namespace Lorebound.Api.Dtos.Settings;

public record SettingListItemDto(
    Guid Id,
    string Name,
    string? Description,
    int EntryCount,
    DateTimeOffset UpdatedAt,
    string MyRole,
    bool IsOwner);