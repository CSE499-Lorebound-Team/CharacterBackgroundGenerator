using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Characters;
using Lorebound.Api.Dtos.Common;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Controllers;

/// <summary>
/// The GameMaster view of the characters players built in a setting (P6-08).
/// Read only: GameMasters have no write endpoint for other people's
/// characters (see <see cref="ICharacterAccess"/>).
/// </summary>
[ApiController]
[Route("api/settings/{settingId:guid}/characters")]
public class SettingCharactersController : ControllerBase
{
  private readonly LoreboundDbContext _db;
  private readonly ISettingAccess _settingAccess;

  public SettingCharactersController(
      LoreboundDbContext db,
      ISettingAccess settingAccess)
  {
    _db = db;
    _settingAccess = settingAccess;
  }

  /// <summary>
  /// Every character in the setting, removed players' included, most
  /// recently updated first. GameMasters only (Player 403, non-member 404).
  /// Filters: <paramref name="status"/>, and <paramref name="search"/>
  /// (character or owner name contains, ignoring case).
  /// </summary>
  [HttpGet]
  [ProducesResponseType<PagedResult<SettingCharacterListItemDto>>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PagedResult<SettingCharacterListItemDto>>> List(
      Guid settingId,
      [FromQuery] CharacterStatus? status,
      [FromQuery] string? search,
      [FromQuery] PageQuery pageQuery,
      CancellationToken cancellationToken)
  {
    await _settingAccess.RequireGameMasterAsync(settingId, cancellationToken);

    var query = _db.Characters
        .AsNoTracking()
        .Where(c => c.CampaignSettingId == settingId);

    if (status is { } characterStatus)
    {
      query = query.Where(c => c.Status == characterStatus);
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
      var pattern = LikePatterns.Contains(search.Trim());

      query = query.Where(c =>
          EF.Functions.ILike(c.Name, pattern, LikePatterns.EscapeCharacter) ||
          EF.Functions.ILike(c.Owner.DisplayName, pattern, LikePatterns.EscapeCharacter));
    }

    var totalCount = await query.CountAsync(cancellationToken);

    // OwnerIsMember matches ICharacterAccess: the setting owner always
    // counts as a member.
    var items = await query
        .OrderByDescending(c => c.UpdatedAt)
        .ThenBy(c => c.Id)
        .Skip(pageQuery.Skip)
        .Take(pageQuery.PageSize)
        .Select(c => new SettingCharacterListItemDto(
            c.Id,
            c.Name,
            c.Status,
            c.OwnerUserId,
            c.Owner.DisplayName,
            c.CampaignSetting.OwnerUserId == c.OwnerUserId
                || c.CampaignSetting.Memberships.Any(m => m.UserId == c.OwnerUserId),
            c.Choices
                .Where(choice => choice.StepKey == CharacterStepKeys.Homeland)
                .OrderBy(choice => choice.Ordinal)
                .Select(choice => choice.Entry != null ? choice.Entry.Name : choice.FreeText)
                .FirstOrDefault(),
            c.CurrentStep,
            c.UpdatedAt))
        .ToListAsync(cancellationToken);

    return Ok(new PagedResult<SettingCharacterListItemDto>(
        items, pageQuery.Page, pageQuery.PageSize, totalCount));
  }
}
