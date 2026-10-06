namespace Lorebound.Api.Dtos.Settings;

public record SettingDetailDto(
    Guid Id,
    string Name,
    string? Description,
    int EntryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string MyRole,
    bool IsOwner);