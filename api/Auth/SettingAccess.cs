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
    var access = await FindAccessAsync(
        settingId,
        cancellationToken);

    return access?.Role;
  }

  public async Task<CampaignSetting> RequireMemberAsync(
      Guid settingId,
      CancellationToken cancellationToken = default)
  {
    var access = await RequireAccessAsync(
        settingId,
        cancellationToken);

    return access.Setting;
  }

  public async Task<CampaignSetting> RequireGameMasterAsync(
      Guid settingId,
      CancellationToken cancellationToken = default)
  {
    var access = await RequireAccessAsync(
        settingId,
        cancellationToken);

    if (access.Role != SettingRole.GameMaster)
    {
      throw new ForbiddenException(
          "Game Master access is required.");
    }

    return access.Setting;
  }

  public async Task<CampaignSetting> RequireOwnerAsync(
      Guid settingId,
      CancellationToken cancellationToken = default)
  {
    var access = await RequireAccessAsync(
        settingId,
        cancellationToken);

    if (!access.IsOwner)
    {
      throw new ForbiddenException(
          "Only the setting owner can perform this action.");
    }

    return access.Setting;
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

  private async Task<SettingAccessResult> RequireAccessAsync(
      Guid settingId,
      CancellationToken cancellationToken)
  {
    // A missing setting and one the user cannot see look the same, so
    // non-members cannot probe which ids exist.
    return await FindAccessAsync(
            settingId,
            cancellationToken)
        ?? throw new NotFoundException(
            "Setting not found.");
  }

  // One query: the setting (tracked, so callers can edit and save it) plus
  // the current user's membership role. The owner always counts as
  // GameMaster, even if their membership row is missing.
  private async Task<SettingAccessResult?> FindAccessAsync(
      Guid settingId,
      CancellationToken cancellationToken)
  {
    var userId = _currentUser.UserId;

    var row = await _db.CampaignSettings
        .Where(setting => setting.Id == settingId)
        .Select(setting => new
        {
          Setting = setting,
          IsOwner = setting.OwnerUserId == userId,
          Role = setting.Memberships
              .Where(membership => membership.UserId == userId)
              .Select(membership => (SettingRole?)membership.Role)
              .FirstOrDefault(),
        })
        .SingleOrDefaultAsync(cancellationToken);

    if (row is null)
    {
      return null;
    }

    if (row.IsOwner)
    {
      return new SettingAccessResult(row.Setting, SettingRole.GameMaster, true);
    }

    return row.Role is { } role
        ? new SettingAccessResult(row.Setting, role, false)
        : null;
  }

  private sealed record SettingAccessResult(
      CampaignSetting Setting,
      SettingRole Role,
      bool IsOwner);
}
