namespace Lorebound.Api.Models;

/// <summary>
/// One answer to a builder step: either a setting entry or free text, never
/// both and never neither. <see cref="Ordinal"/> orders several answers to
/// the same step. Deleting the entry leaves the choice with a null
/// <see cref="EntryId"/> (P6-09).
/// </summary>
public class CharacterChoice
{
  public const int StepKeyMaxLength = 40;

  public const int FreeTextMaxLength = 2000;

  public Guid Id { get; set; }

  public Guid CharacterId { get; set; }

  public Character Character { get; set; } = null!;

  public string StepKey { get; set; } = string.Empty;

  public int Ordinal { get; set; }

  public Guid? EntryId { get; set; }

  public SettingEntry? Entry { get; set; }

  public string? FreeText { get; set; }
}
