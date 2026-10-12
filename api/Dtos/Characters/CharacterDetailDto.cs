using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Characters;

/// <summary>
/// One character with its builder choices (P6-05), returned by create and
/// read. <see cref="IsOwner"/> says whether the caller owns it (only the
/// owner may delete); <see cref="IsReadOnly"/> says whether the caller may
/// not edit it (false only for an owner who still belongs to the setting).
/// <see cref="StaleSteps"/> lists, in step order, the keys of steps whose
/// chosen entry is no longer among that step's narrowed options after an
/// earlier step changed (P7-04); the choice is kept, never deleted.
/// </summary>
public record CharacterDetailDto(
    Guid Id,
    Guid SettingId,
    string SettingName,
    Guid OwnerUserId,
    string OwnerDisplayName,
    string Name,
    CharacterStatus Status,
    int CurrentStep,
    string? Backstory,
    bool IsOwner,
    bool IsReadOnly,
    IReadOnlyList<CharacterChoiceDto> Choices,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> StaleSteps);

/// <summary>
/// One builder answer; choices are sorted by step key then ordinal. A chosen
/// entry keeps its name even if it later became GM-only; a deleted entry
/// leaves <see cref="EntryId"/>, <see cref="EntryName"/> and
/// <see cref="EntryType"/> null.
/// </summary>
public record CharacterChoiceDto(
    string StepKey,
    int Ordinal,
    Guid? EntryId,
    string? EntryName,
    SettingEntryType? EntryType,
    string? FreeText);
