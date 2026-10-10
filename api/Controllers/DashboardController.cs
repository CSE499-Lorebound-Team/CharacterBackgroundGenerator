using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Dashboard;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Controllers;

/// <summary>
/// The dashboard page in one request (P8-01): counts, the most recently
/// updated settings and characters, and recent activity across them.
/// </summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
  public const int RecentSettingsCount = 5;

  public const int RecentCharactersCount = 5;

  public const int RecentActivityCount = 10;

  private readonly LoreboundDbContext _db;
  private readonly ISettingAccess _settingAccess;
  private readonly ICurrentUser _currentUser;

  public DashboardController(
      LoreboundDbContext db,
      ISettingAccess settingAccess,
      ICurrentUser currentUser)
  {
    _db = db;
    _settingAccess = settingAccess;
    _currentUser = currentUser;
  }

  /// <summary>
  /// Counts, the 5 most recently updated settings the caller belongs to and
  /// characters they own, and the 10 most recent changes across those
  /// settings, their visible entries and the caller's characters. Players
  /// never see activity on GM-only entries.
  /// </summary>
  [HttpGet]
  [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
  public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken)
  {
    var userId = _currentUser.UserId;
    var settings = _settingAccess.VisibleToCurrentUser().AsNoTracking();
    var characters = _db.Characters.AsNoTracking().Where(c => c.OwnerUserId == userId);

    // The owner counts as GameMaster even without a membership row, as in
    // the Settings list's role filter.
    var settingsAsGm = await settings.CountAsync(
        setting => setting.OwnerUserId == userId ||
            setting.Memberships.Any(m => m.UserId == userId && m.Role == SettingRole.GameMaster),
        cancellationToken);

    var settingsAsPlayer = await settings.CountAsync(
        setting => setting.OwnerUserId != userId &&
            setting.Memberships.Any(m => m.UserId == userId && m.Role == SettingRole.Player),
        cancellationToken);

    var characterCounts = await characters
        .GroupBy(c => c.Status)
        .Select(group => new { Status = group.Key, Count = group.Count() })
        .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

    var recentSettings = await settings
        .OrderByDescending(setting => setting.UpdatedAt)
        .ThenBy(setting => setting.Id)
        .Take(RecentSettingsCount)
        .ToListItems(userId)
        .ToListAsync(cancellationToken);

    var recentCharacters = await characters
        .OrderByDescending(c => c.UpdatedAt)
        .ThenBy(c => c.Id)
        .Take(RecentCharactersCount)
        .ToListItems(userId)
        .ToListAsync(cancellationToken);

    var recentActivity = await LoadRecentActivityAsync(userId, settings, characters, cancellationToken);

    return Ok(new DashboardDto(
        new DashboardCountsDto(
            settingsAsGm,
            settingsAsPlayer,
            characterCounts.GetValueOrDefault(CharacterStatus.Draft),
            characterCounts.GetValueOrDefault(CharacterStatus.Complete)),
        recentSettings,
        recentCharacters,
        recentActivity));
  }

  // One small query per source, each capped at the final count, merged in
  // memory. A row whose UpdatedAt equals its CreatedAt has never been edited.
  private async Task<List<DashboardActivityDto>> LoadRecentActivityAsync(
      Guid userId,
      IQueryable<CampaignSetting> settings,
      IQueryable<Character> characters,
      CancellationToken cancellationToken)
  {
    var settingRows = await settings
        .OrderByDescending(setting => setting.UpdatedAt)
        .Take(RecentActivityCount)
        .Select(setting => new
        {
          setting.Id,
          setting.Name,
          setting.CreatedAt,
          setting.UpdatedAt,
        })
        .ToListAsync(cancellationToken);

    // The per-setting role decides visibility here, so this cannot use
    // SettingEntryQueries.VisibleTo, which takes a single role (P4-01).
    var entryRows = await _db.SettingEntries
        .AsNoTracking()
        .Where(entry => settings.Any(setting => setting.Id == entry.CampaignSettingId))
        .Where(entry =>
            !entry.IsGmOnly ||
            entry.CampaignSetting.OwnerUserId == userId ||
            entry.CampaignSetting.Memberships.Any(m =>
                m.UserId == userId && m.Role == SettingRole.GameMaster))
        .OrderByDescending(entry => entry.UpdatedAt)
        .Take(RecentActivityCount)
        .Select(entry => new
        {
          entry.Id,
          entry.CampaignSettingId,
          entry.Name,
          SettingName = entry.CampaignSetting.Name,
          entry.CreatedAt,
          entry.UpdatedAt,
        })
        .ToListAsync(cancellationToken);

    var characterRows = await characters
        .OrderByDescending(c => c.UpdatedAt)
        .Take(RecentActivityCount)
        .Select(c => new
        {
          c.Id,
          c.CampaignSettingId,
          c.Name,
          c.CreatedAt,
          c.UpdatedAt,
        })
        .ToListAsync(cancellationToken);

    return settingRows
        .Select(s => new DashboardActivityDto(
            DashboardActivityKind.Setting,
            s.UpdatedAt == s.CreatedAt ? $"Created {s.Name}" : $"Edited {s.Name}",
            s.UpdatedAt,
            s.Id,
            s.Id))
        .Concat(entryRows.Select(e => new DashboardActivityDto(
            DashboardActivityKind.Entry,
            e.UpdatedAt == e.CreatedAt ? $"Added {e.Name} to {e.SettingName}" : $"Edited {e.Name}",
            e.UpdatedAt,
            e.Id,
            e.CampaignSettingId)))
        .Concat(characterRows.Select(c => new DashboardActivityDto(
            DashboardActivityKind.Character,
            c.UpdatedAt == c.CreatedAt ? $"Started {c.Name}" : $"Updated {c.Name}",
            c.UpdatedAt,
            c.Id,
            c.CampaignSettingId)))
        .OrderByDescending(activity => activity.At)
        .Take(RecentActivityCount)
        .ToList();
  }
}
