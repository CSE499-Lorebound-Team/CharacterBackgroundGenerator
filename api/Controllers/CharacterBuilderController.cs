using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Builder;
using Lorebound.Api.Dtos.Characters;
using Lorebound.Api.Errors;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

  /// <summary>
  /// Replaces one step's answer in a single save and returns the updated
  /// character. Entries must be of the step's type, in the character's
  /// setting and visible to the caller, unless already chosen for this step;
  /// a missing, other-setting or hidden entry gets the same 400. Sending no
  /// entries and no text clears the step. Moves <c>currentStep</c> past the
  /// step if it was behind. A complete character must be reopened first
  /// (409).
  /// </summary>
  [HttpPut("choices/{stepKey}")]
  [ProducesResponseType<CharacterDetailDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<CharacterDetailDto>> SaveChoice(
      Guid characterId,
      string stepKey,
      SaveChoiceRequest request,
      CancellationToken cancellationToken)
  {
    var access = await _characterAccess.RequireWriteAsync(characterId, cancellationToken);
    var step = RequireStep(stepKey);
    var character = access.Character;

    if (character.Status == CharacterStatus.Complete)
    {
      throw new ConflictException("This character is complete. Reopen it to change its answers.");
    }

    var entryIds = request.EntryIds ?? [];
    var freeText = string.IsNullOrWhiteSpace(request.FreeText) ? null : request.FreeText.Trim();

    if (!step.StoresChoices)
    {
      ModelState.AddModelError(nameof(stepKey), "This step does not take answers.");
      return ValidationProblem(ModelState);
    }

    if (step.EntryType is null && entryIds.Count > 0)
    {
      ModelState.AddModelError(nameof(request.EntryIds), "This step takes free text, not entries.");
    }

    if (!step.AllowFreeText && freeText is not null)
    {
      ModelState.AddModelError(nameof(request.FreeText), "This step takes entries, not free text.");
    }

    if (freeText is { Length: > CharacterChoice.FreeTextMaxLength })
    {
      ModelState.AddModelError(nameof(request.FreeText),
          $"The text can be at most {CharacterChoice.FreeTextMaxLength} characters.");
    }

    if (entryIds.Count > step.MaxSelections)
    {
      ModelState.AddModelError(nameof(request.EntryIds),
          $"This step takes at most {step.MaxSelections} {(step.MaxSelections == 1 ? "entry" : "entries")}.");
    }
    else if (entryIds.Distinct().Count() != entryIds.Count)
    {
      ModelState.AddModelError(nameof(request.EntryIds), "Each entry can be chosen only once.");
    }

    if (!ModelState.IsValid)
    {
      return ValidationProblem(ModelState);
    }

    var existing = await _db.CharacterChoices
        .Where(choice => choice.CharacterId == character.Id && choice.StepKey == step.Key)
        .ToListAsync(cancellationToken);

    if (step.EntryType is { } entryType && entryIds.Count > 0)
    {
      var role = await RequireRoleAsync(character.CampaignSettingId, cancellationToken);
      var alreadyChosen = existing
          .Where(choice => choice.EntryId is not null)
          .Select(choice => choice.EntryId!.Value)
          .ToArray();
      var requestedIds = entryIds.ToArray();

      // Scoped to the character's setting; an entry the caller may not see
      // counts only if it is already this step's answer (it may have become
      // GM-only since). Missing, other-setting and hidden look the same.
      var entries = await _db.SettingEntries
          .AsNoTracking()
          .Where(entry => entry.CampaignSettingId == character.CampaignSettingId
              && requestedIds.Contains(entry.Id)
              && (role == SettingRole.GameMaster || !entry.IsGmOnly || alreadyChosen.Contains(entry.Id)))
          .ToDictionaryAsync(entry => entry.Id, cancellationToken);

      for (var i = 0; i < entryIds.Count; i++)
      {
        var field = $"{nameof(request.EntryIds)}[{i}]";

        if (!entries.TryGetValue(entryIds[i], out var entry))
        {
          ModelState.AddModelError(field, "Entry not found in this setting.");
        }
        else if (entry.EntryType != entryType)
        {
          ModelState.AddModelError(field, $"This step takes a {entryType} entry, not a {entry.EntryType}.");
        }
      }

      if (!ModelState.IsValid)
      {
        return ValidationProblem(ModelState);
      }
    }

    _db.CharacterChoices.RemoveRange(existing);
    _db.CharacterChoices.AddRange(
        entryIds.Select((entryId, ordinal) => NewChoice(character, step, ordinal, entryId, null)));
    if (freeText is not null)
    {
      _db.CharacterChoices.Add(NewChoice(character, step, 0, null, freeText));
    }

    character.CurrentStep = Math.Max(character.CurrentStep, step.Order + 1);
    // Saving always counts as an update of the character (the list sorts by
    // UpdatedAt), even when the answer and step are unchanged.
    _db.Entry(character).Property(c => c.CurrentStep).IsModified = true;

    try
    {
      await _db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException error) when (error.InnerException is PostgresException
    {
      SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation,
    })
    {
      // Another save of this step, or the deletion of a chosen entry, landed
      // in between.
      throw new ConflictException("This step changed while saving. Try again.");
    }

    return Ok(await _db.LoadCharacterDetailAsync(access, cancellationToken));
  }

  /// <summary>
  /// Marks the character Complete once every required step has a valid
  /// answer, every chosen entry still exists in the setting with the step's
  /// type, no step is stale (P7-04) and the name is not blank. Otherwise 400
  /// with one error per failing step, keyed by step key. Completing a
  /// complete character again is a no-op.
  /// </summary>
  [HttpPost("complete")]
  [ProducesResponseType<CharacterDetailDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<CharacterDetailDto>> Complete(
      Guid characterId,
      CancellationToken cancellationToken)
  {
    var access = await _characterAccess.RequireWriteAsync(characterId, cancellationToken);
    var character = access.Character;

    if (string.IsNullOrWhiteSpace(character.Name))
    {
      ModelState.AddModelError(nameof(Character.Name), "Give the character a name.");
    }

    var choices = await _db.CharacterChoices
        .AsNoTracking()
        .Where(choice => choice.CharacterId == character.Id)
        .Select(choice => new
        {
          choice.StepKey,
          choice.EntryId,
          choice.FreeText,
          EntrySettingId = choice.Entry == null ? (Guid?)null : choice.Entry.CampaignSettingId,
          EntryType = choice.Entry == null ? (SettingEntryType?)null : choice.Entry.EntryType,
        })
        .ToListAsync(cancellationToken);

    var ownerRole = await _db.LoadOwnerRoleAsync(character.CampaignSettingId, character.OwnerUserId, cancellationToken);
    var staleSteps = await _db.LoadStaleStepsAsync(character.Id, character.CampaignSettingId, ownerRole, cancellationToken);

    foreach (var step in BuilderSteps.All.Where(step => step.StoresChoices))
    {
      // A choice whose entry was deleted has neither an entry nor text, so
      // it does not answer the step.
      var answers = choices
          .Where(choice => choice.StepKey == step.Key && (choice.EntryId is not null || choice.FreeText is not null))
          .ToList();

      if (answers.Any(answer => answer.EntryId is not null
          && (answer.EntrySettingId != character.CampaignSettingId || answer.EntryType != step.EntryType)))
      {
        ModelState.AddModelError(step.Key, "The chosen entry no longer fits this step. Choose again.");
      }
      else if (staleSteps.Contains(step.Key))
      {
        ModelState.AddModelError(step.Key, "This answer no longer fits your earlier choices. Choose again.");
      }
      else if (step.Required && answers.Count == 0)
      {
        ModelState.AddModelError(step.Key, $"{step.Title} needs an answer.");
      }
    }

    if (!ModelState.IsValid)
    {
      return ValidationProblem(ModelState);
    }

    if (character.Status != CharacterStatus.Complete)
    {
      character.Status = CharacterStatus.Complete;
      await _db.SaveChangesAsync(cancellationToken);
    }

    return Ok(await _db.LoadCharacterDetailAsync(access, cancellationToken));
  }

  /// <summary>
  /// Moves the character back to Draft so its answers can change again.
  /// Reopening a draft is a no-op.
  /// </summary>
  [HttpPost("reopen")]
  [ProducesResponseType<CharacterDetailDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<CharacterDetailDto>> Reopen(
      Guid characterId,
      CancellationToken cancellationToken)
  {
    var access = await _characterAccess.RequireWriteAsync(characterId, cancellationToken);
    var character = access.Character;

    if (character.Status != CharacterStatus.Draft)
    {
      character.Status = CharacterStatus.Draft;
      await _db.SaveChangesAsync(cancellationToken);
    }

    return Ok(await _db.LoadCharacterDetailAsync(access, cancellationToken));
  }

  private static CharacterChoice NewChoice(
      Character character, BuilderStep step, int ordinal, Guid? entryId, string? freeText) =>
      new()
      {
        Id = Guid.NewGuid(),
        CharacterId = character.Id,
        StepKey = step.Key,
        Ordinal = ordinal,
        EntryId = entryId,
        FreeText = freeText,
      };

  private static BuilderStep RequireStep(string stepKey) =>
      BuilderSteps.Find(stepKey) ?? throw new NotFoundException("Builder step not found.");

  // Write access means the caller is a member, so the role is known; the
  // setting owner counts as a GameMaster.
  private async Task<SettingRole> RequireRoleAsync(Guid settingId, CancellationToken cancellationToken) =>
      await _settingAccess.GetRoleAsync(settingId, cancellationToken)
      ?? throw new NotFoundException("Character not found.");
}
