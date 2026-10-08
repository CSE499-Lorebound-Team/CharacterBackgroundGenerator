using Lorebound.Api.Models;

namespace Lorebound.Api.Data;

public static class SettingEntryQueries
{
  /// <summary>
  /// The entries <paramref name="role"/> may see: everything for a
  /// GameMaster, everything but <see cref="SettingEntry.IsGmOnly"/> for a
  /// Player. Every query that reads entries for a response goes through this
  /// (P4-01), so secret lore cannot leak through a forgotten filter.
  /// </summary>
  public static IQueryable<SettingEntry> VisibleTo(
      this IQueryable<SettingEntry> entries,
      SettingRole role) =>
      role == SettingRole.GameMaster
          ? entries
          : entries.Where(entry => !entry.IsGmOnly);
}
