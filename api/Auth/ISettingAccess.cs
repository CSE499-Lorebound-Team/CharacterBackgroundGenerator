using Lorebound.Api.Models;

namespace Lorebound.Api.Auth;

/// <summary>
/// The one place that decides what the current user may do in a setting.
/// Every endpoint that touches setting data calls it first. A missing setting
/// and a setting the user is not a member of both throw
/// <see cref="Errors.NotFoundException"/> (404); a member without the needed
/// role gets <see cref="Errors.ForbiddenException"/> (403). The owner always
/// counts as a GameMaster.
/// </summary>
public interface ISettingAccess
{
  /// <summary>The current user's role, or null for a non-member or missing setting.</summary>
  Task<SettingRole?> GetRoleAsync(
      Guid settingId,
      CancellationToken cancellationToken = default);

  /// <summary>Any member (Player, GameMaster or owner). Returns the tracked setting.</summary>
  Task<CampaignSetting> RequireMemberAsync(
      Guid settingId,
      CancellationToken cancellationToken = default);

  /// <summary>A GameMaster or the owner; a Player gets 403.</summary>
  Task<CampaignSetting> RequireGameMasterAsync(
      Guid settingId,
      CancellationToken cancellationToken = default);

  /// <summary>Only the owner; any other member gets 403.</summary>
  Task<CampaignSetting> RequireOwnerAsync(
      Guid settingId,
      CancellationToken cancellationToken = default);

  /// <summary>Settings the current user owns or belongs to, for list queries.</summary>
  IQueryable<CampaignSetting> VisibleToCurrentUser();
}
