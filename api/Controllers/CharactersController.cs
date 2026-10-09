using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Characters;
using Lorebound.Api.Errors;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Controllers;

/// <summary>
/// Characters (Phase 6). Every action on an existing character goes through
/// <see cref="ICharacterAccess"/>: the owner reads and writes while still a
/// member of the setting, a removed owner reads and deletes only, and the
/// setting's GameMasters read only.
/// </summary>
[ApiController]
[Route("api/characters")]
public class CharactersController : ControllerBase
{
  private readonly LoreboundDbContext _db;
  private readonly ISettingAccess _settingAccess;
  private readonly ICharacterAccess _characterAccess;
  private readonly ICurrentUser _currentUser;

  public CharactersController(
      LoreboundDbContext db,
      ISettingAccess settingAccess,
      ICharacterAccess characterAccess,
      ICurrentUser currentUser)
  {
    _db = db;
    _settingAccess = settingAccess;
    _characterAccess = characterAccess;
    _currentUser = currentUser;
  }

  /// <summary>
  /// Starts a draft character owned by the caller, on step 1. Any member of
  /// the setting may create one, GameMasters included (NPCs or their own
  /// PC); a non-member or missing setting gets 404.
  /// </summary>
  [HttpPost]
  [ProducesResponseType<CharacterDetailDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<CharacterDetailDto>> Create(
      CreateCharacterRequest request,
      CancellationToken cancellationToken)
  {
    var settingId = request.SettingId!.Value;
    await _settingAccess.RequireMemberAsync(settingId, cancellationToken);

    var character = new Character
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = settingId,
      OwnerUserId = _currentUser.UserId,
      Name = string.IsNullOrWhiteSpace(request.Name)
          ? Character.DefaultName
          : request.Name.Trim(),
      Status = CharacterStatus.Draft,
      CurrentStep = 1,
    };

    _db.Characters.Add(character);
    await _db.SaveChangesAsync(cancellationToken);

    var detail = await LoadDetailAsync(
        new CharacterAccessResult(character, IsOwner: true, IsReadOnly: false),
        cancellationToken);

    return Created($"/api/characters/{character.Id}", detail);
  }

  /// <summary>
  /// One character with its choices. The owner (even read-only) and the
  /// setting's GameMasters may read it; anyone else gets 404.
  /// </summary>
  [HttpGet("{characterId:guid}")]
  [ProducesResponseType<CharacterDetailDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<CharacterDetailDto>> Get(
      Guid characterId,
      CancellationToken cancellationToken)
  {
    var access = await _characterAccess.RequireReadAsync(characterId, cancellationToken);

    return Ok(await LoadDetailAsync(access, cancellationToken));
  }

  // Choices are not filtered by IsGmOnly: a character keeps showing an entry
  // that became GM-only after it was chosen (decision in P6-05), and only
  // the owner and GameMasters can read a character anyway.
  private async Task<CharacterDetailDto> LoadDetailAsync(
      CharacterAccessResult access,
      CancellationToken cancellationToken)
  {
    var characterId = access.Character.Id;
    var isOwner = access.IsOwner;
    var isReadOnly = access.IsReadOnly;

    var detail = await _db.Characters
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
            c.UpdatedAt))
        .SingleOrDefaultAsync(cancellationToken);

    // Deleted between the access check and this read.
    return detail ?? throw new NotFoundException("Character not found.");
  }
}
