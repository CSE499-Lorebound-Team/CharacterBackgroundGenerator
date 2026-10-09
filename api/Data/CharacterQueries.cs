using Lorebound.Api.Auth;
using Lorebound.Api.Dtos.Characters;
using Lorebound.Api.Errors;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Data;

public static class CharacterQueries
{
  /// <summary>
  /// The character in <paramref name="access"/> with its choices, as every
  /// character endpoint returns it. Choices are not filtered by
  /// <c>IsGmOnly</c>: a character keeps showing an entry that became GM-only
  /// after it was chosen (decision in P6-05), and only the owner and
  /// GameMasters can read a character anyway.
  /// </summary>
  public static async Task<CharacterDetailDto> LoadCharacterDetailAsync(
      this LoreboundDbContext db,
      CharacterAccessResult access,
      CancellationToken cancellationToken = default)
  {
    var characterId = access.Character.Id;
    var isOwner = access.IsOwner;
    var isReadOnly = access.IsReadOnly;

    var detail = await db.Characters
        .AsNoTracking()
        .Where(c => c.Id == characterId)
        .Select(c => new CharacterDetailDto(
            c.Id,
            c.CampaignSettingId,
            c.CampaignSetting.Name,
            c.OwnerUserId,
            c.Owner.DisplayName,
            c.Name,
            c.Status,
            c.CurrentStep,
            c.Backstory,
            isOwner,
            isReadOnly,
            c.Choices
                .OrderBy(choice => choice.StepKey)
                .ThenBy(choice => choice.Ordinal)
                .Select(choice => new CharacterChoiceDto(
                    choice.StepKey,
                    choice.Ordinal,
                    choice.EntryId,
                    choice.Entry == null ? null : choice.Entry.Name,
                    choice.Entry == null ? null : choice.Entry.EntryType,
                    choice.FreeText))
                .ToList(),
            c.CreatedAt,
            c.UpdatedAt,
            Array.Empty<string>()))
        .SingleOrDefaultAsync(cancellationToken);

    // Deleted between the access check and this read.
    if (detail is null)
    {
      throw new NotFoundException("Character not found.");
    }

    // Stale steps are judged by the owner's view of the setting, whoever
    // reads the character (P7-04).
    var ownerRole = await db.LoadOwnerRoleAsync(detail.SettingId, detail.OwnerUserId, cancellationToken);
    var staleSteps = await db.LoadStaleStepsAsync(detail.Id, detail.SettingId, ownerRole, cancellationToken);

    return detail with { StaleSteps = staleSteps };
  }
}
