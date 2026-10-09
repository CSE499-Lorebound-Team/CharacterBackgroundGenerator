using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Builder;
using Lorebound.Api.Errors;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Controllers;

/// <summary>
/// The guided builder for one character (Phase 7). Only the owner while
/// still a member of the setting may use it (<see cref="ICharacterAccess.RequireWriteAsync"/>):
/// a removed owner or a GameMaster gets 403, anyone else 404.
/// </summary>
[ApiController]
[Route("api/characters/{characterId:guid}")]
public class CharacterBuilderController : ControllerBase
{
  private readonly LoreboundDbContext _db;
  private readonly ISettingAccess _settingAccess;
  private readonly ICharacterAccess _characterAccess;

  public CharacterBuilderController(
      LoreboundDbContext db,
      ISettingAccess settingAccess,
      ICharacterAccess characterAccess)
  {
    _db = db;
    _settingAccess = settingAccess;
    _characterAccess = characterAccess;
  }

  /// <summary>
  /// The options for one step, narrowed by the entries chosen in earlier
  /// steps (ADR 0002). Free-text and display-only steps have none. An
  /// unknown step is 404.
  /// </summary>
  [HttpGet("steps/{stepKey}/options")]
  [ProducesResponseType<StepOptionsDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<StepOptionsDto>> GetOptions(
      Guid characterId,
      string stepKey,
      CancellationToken cancellationToken)
  {
    var access = await _characterAccess.RequireWriteAsync(characterId, cancellationToken);
    var step = RequireStep(stepKey);
    var character = access.Character;
    var role = await RequireRoleAsync(character.CampaignSettingId, cancellationToken);

    var options = await _db.LoadOptionsAsync(
        character.Id, character.CampaignSettingId, step, role, cancellationToken);

    return Ok(new StepOptionsDto(
        step.Key,
        options.Narrowed,
        options.Options.Select(option => new StepOptionDto(option.EntryId, option.Name, option.Description)).ToList()));
  }

  private static BuilderStep RequireStep(string stepKey) =>
      BuilderSteps.Find(stepKey) ?? throw new NotFoundException("Builder step not found.");

  // Write access means the caller is a member, so the role is known; the
  // setting owner counts as a GameMaster.
  private async Task<SettingRole> RequireRoleAsync(Guid settingId, CancellationToken cancellationToken) =>
      await _settingAccess.GetRoleAsync(settingId, cancellationToken)
      ?? throw new NotFoundException("Character not found.");
}
