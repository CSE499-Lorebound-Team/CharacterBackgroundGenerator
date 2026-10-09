namespace Lorebound.Api.Models;

/// <summary>
/// A player's character in one setting, built step by step (P7). Builder
/// answers are <see cref="CharacterChoice"/> rows, so adding a step needs no
/// migration. <see cref="Backstory"/> is free text only; nothing generates it.
/// </summary>
public class Character : ITimestamped
{
  public const int NameMaxLength = 100;

  public const int BackstoryMaxLength = 10000;

  public const string DefaultName = "Unnamed Character";

  public Guid Id { get; set; }

  public Guid CampaignSettingId { get; set; }

  public CampaignSetting CampaignSetting { get; set; } = null!;

  public Guid OwnerUserId { get; set; }

  public ApplicationUser Owner { get; set; } = null!;

  public string Name { get; set; } = DefaultName;

  public CharacterStatus Status { get; set; } = CharacterStatus.Draft;

  /// <summary>The builder step the owner is on, starting at 1.</summary>
  public int CurrentStep { get; set; } = 1;

  public string? Backstory { get; set; }

  public ICollection<CharacterChoice> Choices { get; set; }
      = new List<CharacterChoice>();

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }
}
