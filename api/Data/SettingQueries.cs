using Lorebound.Api.Dtos.Settings;
using Lorebound.Api.Models;

namespace Lorebound.Api.Data;

public static class SettingQueries
{
  /// <summary>
  /// Settings as the Settings page and dashboard list them, seen by
  /// <paramref name="userId"/>, who must own or belong to each one (use
  /// <see cref="Auth.ISettingAccess.VisibleToCurrentUser"/>). Players do not
  /// count GM-only entries (P4-01).
  /// </summary>
  public static IQueryable<SettingListItemDto> ToListItems(
      this IQueryable<CampaignSetting> settings,
      Guid userId) =>
      settings.Select(setting => new SettingListItemDto(
          setting.Id,
          setting.Name,
          setting.Description,
          setting.Entries.Count(entry =>
              !entry.IsGmOnly ||
              setting.OwnerUserId == userId ||
              setting.Memberships.Any(membership =>
                  membership.UserId == userId &&
                  membership.Role == SettingRole.GameMaster)),
          setting.OwnerUserId == userId
              ? "GameMaster"
              : setting.Memberships
                  .Where(membership => membership.UserId == userId)
                  .Select(membership => membership.Role.ToString())
                  .First(),
          setting.OwnerUserId == userId,
          setting.Owner.DisplayName,
          setting.UpdatedAt));
}
