using Lorebound.Api.Dtos.Entries;
using Lorebound.Api.Models;

namespace Lorebound.Api.Mapping;

public static class SettingEntryMappings
{
  /// <summary>
  /// <see cref="SettingEntryDto.IsGmOnly"/> is filled in for a GameMaster
  /// only; Players never learn the flag exists.
  /// </summary>
  public static SettingEntryDto ToDto(this SettingEntry entry, SettingRole role) => new(
      entry.Id,
      entry.CampaignSettingId,
      entry.Name,
      entry.Description,
      entry.EntryType,
      role == SettingRole.GameMaster ? entry.IsGmOnly : null,
      entry.CreatedAt,
      entry.UpdatedAt);
}
