using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Characters;

/// <summary>
/// One character of a setting in the GameMaster view (P6-08).
/// <see cref="OwnerIsMember"/> is false for characters of players who were
/// removed from the setting; they stay listed. <see cref="HomelandName"/>
/// is as in <see cref="CharacterListItemDto"/>.
/// </summary>
public record SettingCharacterListItemDto(
    Guid Id,
    string Name,
    CharacterStatus Status,
    Guid OwnerUserId,
    string OwnerDisplayName,
    bool OwnerIsMember,
    string? HomelandName,
    int CurrentStep,
    DateTimeOffset UpdatedAt);
