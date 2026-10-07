using Lorebound.Api.Models;

namespace Lorebound.Api.Auth;

public interface ISettingAccess
{
  Task<SettingRole?> GetRoleAsync(
      Guid settingId,
      CancellationToken cancellationToken = default);

  Task<CampaignSetting> RequireMemberAsync(
      Guid settingId,
      CancellationToken cancellationToken = default);

  Task<CampaignSetting> RequireGameMasterAsync(
      Guid settingId,
      CancellationToken cancellationToken = default);

  Task<CampaignSetting> RequireOwnerAsync(
      Guid settingId,
      CancellationToken cancellationToken = default);

  IQueryable<CampaignSetting> VisibleToCurrentUser();
}