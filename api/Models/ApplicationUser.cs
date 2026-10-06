using Microsoft.AspNetCore.Identity;

namespace Lorebound.Api.Models;

public class ApplicationUser : IdentityUser<Guid>, ICreatedAt
{
  public string DisplayName { get; set; } = string.Empty;

  public DateTimeOffset CreatedAt { get; set; }

  public ICollection<CampaignSetting> CampaignSettings { get; set; }
      = new List<CampaignSetting>();

      public ICollection<SettingMembership> SettingMemberships { get; set; }
    = new List<SettingMembership>();
}
