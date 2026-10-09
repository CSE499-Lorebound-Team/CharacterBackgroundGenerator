namespace Lorebound.Api.Dtos.Builder;

/// <summary>
/// One step's answer (P7-03): entries for an entry step, free text for a
/// free-text step. Sending neither clears the step.
/// </summary>
public record SaveChoiceRequest(
    IReadOnlyList<Guid>? EntryIds,
    string? FreeText);
