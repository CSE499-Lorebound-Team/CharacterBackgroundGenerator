using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Characters;
using Lorebound.Api.Dtos.Common;
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
  /// The caller's own characters, most recently updated first. Filters:
  /// <paramref name="status"/>, <paramref name="settingId"/>, and
  /// <paramref name="search"/> (name contains, ignoring case). Characters in
  /// a setting the caller has left are included, flagged read-only.
  /// </summary>
  [HttpGet]
  [ProducesResponseType<PagedResult<CharacterListItemDto>>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<ActionResult<PagedResult<CharacterListItemDto>>> List(
      [FromQuery] CharacterStatus? status,
      [FromQuery] Guid? settingId,
      [FromQuery] string? search,
      [FromQuery] PageQuery pageQuery,
      CancellationToken cancellationToken)
  {
    var userId = _currentUser.UserId;

    var query = _db.Characters
        .AsNoTracking()
        .Where(c => c.OwnerUserId == userId);

    if (status is { } characterStatus)
    {
      query = query.Where(c => c.Status == characterStatus);
    }

    if (settingId is { } id)
    {
      query = query.Where(c => c.CampaignSettingId == id);
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
      var pattern = LikePatterns.Contains(search.Trim());

      query = query.Where(c => EF.Functions.ILike(c.Name, pattern, LikePatterns.EscapeCharacter));
    }

    var totalCount = await query.CountAsync(cancellationToken);

    var items = await query
        .OrderByDescending(c => c.UpdatedAt)
        .ThenBy(c => c.Id)
        .Skip(pageQuery.Skip)
        .Take(pageQuery.PageSize)
        .ToListItems(userId)
        .ToListAsync(cancellationToken);

    return Ok(new PagedResult<CharacterListItemDto>(items, pageQuery.Page, pageQuery.PageSize, totalCount));
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

    var detail = await _db.LoadCharacterDetailAsync(
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

    return Ok(await _db.LoadCharacterDetailAsync(access, cancellationToken));
  }

  /// <summary>
  /// Replaces the name and backstory. Only the owner while still a member
  /// of the setting; a removed owner or a GameMaster gets 403. The backstory
  /// is free text, never rendered as HTML by the API.
  /// </summary>
  [HttpPut("{characterId:guid}")]
  [ProducesResponseType<CharacterDetailDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<CharacterDetailDto>> Update(
      Guid characterId,
      UpdateCharacterRequest request,
      CancellationToken cancellationToken)
  {
    var access = await _characterAccess.RequireWriteAsync(characterId, cancellationToken);

    var name = request.Name.Trim();
    if (name.Length == 0)
    {
      ModelState.AddModelError(nameof(request.Name), "The name cannot be blank.");
      return ValidationProblem(ModelState);
    }

    var character = access.Character;
    character.Name = name;
    character.Backstory = string.IsNullOrWhiteSpace(request.Backstory) ? null : request.Backstory;

    // Saving always counts as an update, so UpdatedAt moves even when the
    // values are unchanged (the list sorts by it).
    _db.Entry(character).Property(c => c.Name).IsModified = true;
    await _db.SaveChangesAsync(cancellationToken);

    return Ok(await _db.LoadCharacterDetailAsync(access, cancellationToken));
  }

  /// <summary>
  /// Deletes the character and, through the database cascade, its choices.
  /// Only the owner, also after removal from the setting (a read-only
  /// character can still be deleted); a GameMaster gets 403.
  /// </summary>
  [HttpDelete("{characterId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
      Guid characterId,
      CancellationToken cancellationToken)
  {
    var access = await _characterAccess.RequireOwnerAsync(characterId, cancellationToken);

    _db.Characters.Remove(access.Character);
    await _db.SaveChangesAsync(cancellationToken);

    return NoContent();
  }
}
