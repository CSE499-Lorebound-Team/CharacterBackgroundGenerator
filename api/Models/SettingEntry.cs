namespace Lorebound.Api.Models;

/// <summary>
/// One piece of lore in a setting. <see cref="Name"/> is citext, unique per
/// (setting, type) ignoring case. Read entries through
/// <see cref="Data.SettingEntryQueries.VisibleTo(IQueryable{SettingEntry}, SettingRole)"/> so Players never see
/// <see cref="IsGmOnly"/> ones.
/// </summary>
public class SettingEntry : ITimestamped
{
  public Guid Id { get; set; }

  public Guid CampaignSettingId { get; set; }

  public CampaignSetting CampaignSetting { get; set; } = null!;

  public string Name { get; set; } = string.Empty;

  public string? Description { get; set; }

  public SettingEntryType EntryType { get; set; }

  /// <summary>Secret lore: only GameMasters (and the owner) see it.</summary>
  public bool IsGmOnly { get; set; }

  public ICollection<SettingEntryRelationship> OutgoingRelationships { get; set; }
      = new List<SettingEntryRelationship>();

  public ICollection<SettingEntryRelationship> IncomingRelationships { get; set; }
      = new List<SettingEntryRelationship>();

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }
}
