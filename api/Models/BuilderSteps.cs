namespace Lorebound.Api.Models;

/// <summary>
/// One step of the guided character builder (P7-01). A step takes entries of
/// <see cref="EntryType"/> from the character's setting, or free text when
/// <see cref="AllowFreeText"/>, up to <see cref="MaxSelections"/> answers.
/// A step with neither (setting, review) is shown by the frontend but never
/// stored as a <see cref="CharacterChoice"/>.
/// </summary>
public record BuilderStep(
    string Key,
    int Order,
    string Title,
    string Description,
    SettingEntryType? EntryType,
    bool AllowFreeText,
    bool Required,
    int MaxSelections)
{
  /// <summary>True when answers to this step are saved as choice rows.</summary>
  public bool StoresChoices => EntryType is not null || AllowFreeText;
}

/// <summary>
/// The builder step catalog: the one definition the frontend wizard and
/// choice validation (P7-03, P7-05) both read. Adding a step is a new row
/// here; choices are rows, so no migration is needed.
/// </summary>
public static class BuilderSteps
{
  /// <summary>Served by <c>GET /api/builder/steps</c>, in this order.</summary>
  public static IReadOnlyList<BuilderStep> All { get; } =
  [
    new(CharacterStepKeys.Setting, 1, "Setting",
        "The campaign setting this character belongs to, chosen when the character was created.",
        EntryType: null, AllowFreeText: false, Required: true, MaxSelections: 0),
    new(CharacterStepKeys.Homeland, 2, "Choose Your Homeland",
        "Where your character comes from.",
        SettingEntryType.Location, AllowFreeText: false, Required: true, MaxSelections: 1),
    new(CharacterStepKeys.Culture, 3, "Culture",
        "The culture that shaped your character.",
        SettingEntryType.Culture, AllowFreeText: false, Required: true, MaxSelections: 1),
    new(CharacterStepKeys.Religion, 4, "Religion",
        "A faith your character follows, if any.",
        SettingEntryType.Religion, AllowFreeText: false, Required: false, MaxSelections: 1),
    new(CharacterStepKeys.SocialClass, 5, "Social Class",
        "Your character's place in society, if it matters to their story.",
        SettingEntryType.SocialClass, AllowFreeText: false, Required: false, MaxSelections: 1),
    new(CharacterStepKeys.Profession, 6, "Profession",
        "What your character does for a living.",
        SettingEntryType.Profession, AllowFreeText: false, Required: true, MaxSelections: 1),
    new(CharacterStepKeys.Motivation, 7, "Motivation",
        "What drives your character forward, in your own words.",
        EntryType: null, AllowFreeText: true, Required: true, MaxSelections: 1),
    new(CharacterStepKeys.Review, 8, "Review",
        "Check every answer before completing your character.",
        EntryType: null, AllowFreeText: false, Required: false, MaxSelections: 0),
  ];

  /// <summary>The step with <paramref name="key"/> (exact match), or null.</summary>
  public static BuilderStep? Find(string key) =>
      All.FirstOrDefault(step => step.Key == key);
}
