using Lorebound.Api.Data;
using Lorebound.Api.Errors;
using Lorebound.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Auth;

public class SettingAccess : ISettingAccess
{
  private readonly LoreboundDbContext _db;
  private readonly ICurrentUser _currentUser;

  public SettingAccess(
      LoreboundDbContext db,
      ICurrentUser currentUser)
  {
    _db = db;
    _currentUser = currentUser;
  }

  public async Task<SettingRole?> GetRoleAsync(
      Guid settingId,
      CancellationToken cancellationToken = default)
  {
    return await _db.SettingMemberships
        .AsNoTracking()
        .Where(membership =>
            membership.CampaignSettingId == settingId &&
            membership.UserId == _currentUser.UserId)
        .Select(membership =>
            (SettingRole?)membership.Role)
        .SingleOrDefaultAsync(cancellationToken);
  }

  public async Task<CampaignSetting> RequireMemberAsync(
      Guid settingId,
      CancellationToken cancellationToken = default)
  {
    var setting = await GetVisibleSettingAsync(
        settingId,
        cancellationToken);

    if (setting is null)
    {
      throw new NotFoundException(
          "Setting not found.");
    }

    return setting;
  }

  public async Task<CampaignSetting> RequireGameMasterAsync(
      Guid settingId,
      CancellationToken cancellationToken = default)
  {
    var setting = await GetVisibleSettingAsync(
        settingId,
        cancellationToken);

    if (setting is null)
    {
      throw new NotFoundException(
          "Setting not found.");
    }

    if (setting.OwnerUserId == _currentUser.UserId)
    {
      return setting;
    }

    var role = await GetRoleAsync(
        settingId,
        cancellationToken);

    if (role != SettingRole.GameMaster)
    {
      throw new ForbiddenException(
          "Game Master access is required.");
    }

    return setting;
  }

  public async Task<CampaignSetting> RequireOwnerAsync(
      Guid settingId,
      CancellationToken cancellationToken = default)
  {
    var setting = await GetVisibleSettingAsync(
        settingId,
        cancellationToken);

    if (setting is null)
    {
      throw new NotFoundException(
          "Setting not found.");
    }

    if (setting.OwnerUserId != _currentUser.UserId)
    {
      throw new ForbiddenException(
          "Only the setting owner can perform this action.");
    }

    return setting;
  }

  public IQueryable<CampaignSetting> VisibleToCurrentUser()
  {
    var userId = _currentUser.UserId;

    return _db.CampaignSettings
        .Where(setting =>
            setting.OwnerUserId == userId ||
            setting.Memberships.Any(membership =>
                membership.UserId == userId));
  }

  private async Task<CampaignSetting?> GetVisibleSettingAsync(
      Guid settingId,
      CancellationToken cancellationToken)
  {
    return await VisibleToCurrentUser()
        .SingleOrDefaultAsync(
            setting => setting.Id == settingId,
            cancellationToken);
  }
}