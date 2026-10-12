namespace Lorebound.Api.Dtos.Builder;

/// <summary>
/// The options for one builder step (P7-02). <see cref="Narrowed"/> is true
/// when earlier choices limited them to linked entries.
/// </summary>
public record StepOptionsDto(
    string StepKey,
    bool Narrowed,
    IReadOnlyList<StepOptionDto> Options);

public record StepOptionDto(
    Guid EntryId,
    string Name,
    string? Description);
