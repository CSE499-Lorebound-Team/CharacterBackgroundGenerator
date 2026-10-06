namespace Lorebound.Api.Models;

public class SettingMembership : ICreatedAt
{
  public Guid Id { get; set; }

  public Guid CampaignSettingId { get; set; }

  public CampaignSetting CampaignSetting { get; set; } = null!;

  public Guid UserId { get; set; }

  public ApplicationUser User { get; set; } = null!;

  public SettingRole Role { get; set; }

  public DateTimeOffset CreatedAt { get; set; }
}