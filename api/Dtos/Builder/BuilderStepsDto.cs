using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Builder;

/// <summary>The builder step catalog (P7-01), ordered by <see cref="BuilderStepDto.Order"/>.</summary>
public record BuilderStepsDto(IReadOnlyList<BuilderStepDto> Steps);

/// <summary>
/// One builder step. <see cref="EntryType"/> is the entry type offered as
/// options (null for free-text and display-only steps);
/// <see cref="MaxFreeTextLength"/> is set only when
/// <see cref="AllowFreeText"/>; <see cref="MaxSelections"/> is 0 for steps
/// that store nothing (setting, review).
/// </summary>
public record BuilderStepDto(
    string Key,
    int Order,
    string Title,
    string Description,
    SettingEntryType? EntryType,
    bool AllowFreeText,
    int? MaxFreeTextLength,
    bool Required,
    int MaxSelections)
{
  public static BuilderStepDto From(BuilderStep step) =>
      new(
          step.Key,
          step.Order,
          step.Title,
          step.Description,
          step.EntryType,
          step.AllowFreeText,
          step.AllowFreeText ? CharacterChoice.FreeTextMaxLength : null,
          step.Required,
          step.MaxSelections);
}
