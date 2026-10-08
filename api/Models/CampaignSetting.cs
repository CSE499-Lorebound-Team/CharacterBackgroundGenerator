namespace Lorebound.Api.Models;

public class CampaignSetting : ITimestamped
{
  public Guid Id { get; set; }

  public string Name { get; set; } = string.Empty;

  public string? Description { get; set; }

  public Guid OwnerUserId { get; set; }

  public ApplicationUser Owner { get; set; } = null!;

  public ICollection<SettingEntry> Entries { get; set; }
      = new List<SettingEntry>();

  public ICollection<SettingEntryRelationship> Relationships { get; set; }
      = new List<SettingEntryRelationship>();

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }

  public ICollection<SettingMembership> Memberships { get; set; }
    = new List<SettingMembership>();

  public ICollection<SettingInvite> Invites { get; set; }
      = new List<SettingInvite>();
}