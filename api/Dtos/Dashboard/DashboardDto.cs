using Lorebound.Api.Dtos.Characters;
using Lorebound.Api.Dtos.Settings;

namespace Lorebound.Api.Dtos.Dashboard;

/// <summary>
/// Everything the dashboard page shows, in one response (P8-01).
/// <see cref="RecentSettings"/> and <see cref="RecentCharacters"/> are the
/// same items the Settings and Characters lists return.
/// </summary>
public record DashboardDto(
    DashboardCountsDto Counts,
    IReadOnlyList<SettingListItemDto> RecentSettings,
    IReadOnlyList<CharacterListItemDto> RecentCharacters,
    IReadOnlyList<DashboardActivityDto> RecentActivity);

/// <summary>
/// Settings the caller owns or is a GameMaster of, settings where the caller
/// is a Player, and the caller's own characters by status.
/// </summary>
public record DashboardCountsDto(
    int SettingsAsGm,
    int SettingsAsPlayer,
    int CharactersDraft,
    int CharactersComplete);

public enum DashboardActivityKind
{
  Setting,
  Entry,
  Character
}

/// <summary>
/// One recent change, derived from <c>UpdatedAt</c> (there is no activity
/// log), so it says what changed, not who changed it. <see cref="Id"/> is the
/// setting, entry or character; <see cref="SettingId"/> is the setting it
/// belongs to, for building links.
/// </summary>
public record DashboardActivityDto(
    DashboardActivityKind Kind,
    string Text,
    DateTimeOffset At,
    Guid Id,
    Guid SettingId);
