namespace Lorebound.Api.Models;

/// <summary>
/// A user's access to a setting. The owner always has a GameMaster row too,
/// so "settings I belong to" is one query; one row per (setting, user).
/// </summary>
public class SettingMembership : ITimestamped
{
  public Guid Id { get; set; }

  public Guid CampaignSettingId { get; set; }

  public CampaignSetting CampaignSetting { get; set; } = null!;

  public Guid UserId { get; set; }

  public ApplicationUser User { get; set; } = null!;

  public SettingRole Role { get; set; }

  /// <summary>Defaults to the save time when left unset.</summary>
  public DateTimeOffset JoinedAt { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }
}