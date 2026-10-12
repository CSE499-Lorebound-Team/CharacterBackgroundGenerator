using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Characters;

/// <summary>
/// One of the caller's own characters on the Characters page and dashboard
/// (P6-04). <see cref="HomelandName"/> comes from the first
/// <c>homeland</c> choice (its entry's name, or its free text), null when
/// there is none. <see cref="IsReadOnly"/> is true once the caller is no
/// longer a member of the setting.
/// </summary>
public record CharacterListItemDto(
    Guid Id,
    string Name,
    CharacterStatus Status,
    Guid SettingId,
    string SettingName,
    string? HomelandName,
    int CurrentStep,
    bool IsReadOnly,
    DateTimeOffset UpdatedAt);
