using Lorebound.Api.Data;
using Lorebound.Api.Errors;
using Lorebound.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Auth;

public class CharacterAccess : ICharacterAccess
{
  private readonly LoreboundDbContext _db;
  private readonly ICurrentUser _currentUser;

  public CharacterAccess(
      LoreboundDbContext db,
      ICurrentUser currentUser)
  {
    _db = db;
    _currentUser = currentUser;
  }

  public Task<CharacterAccessResult> RequireReadAsync(
      Guid characterId,
      CancellationToken cancellationToken = default)
  {
    return RequireAccessAsync(characterId, cancellationToken);
  }

  public async Task<CharacterAccessResult> RequireWriteAsync(
      Guid characterId,
      CancellationToken cancellationToken = default)
  {
    var access = await RequireAccessAsync(characterId, cancellationToken);

    if (!access.IsOwner)
    {
      throw new ForbiddenException(
          "Only the character's owner can edit it.");
    }

    if (access.IsReadOnly)
    {
      throw new ForbiddenException(
          "This character is read-only because you are no longer a member of its setting.");
    }

    return access;
  }

  public async Task<CharacterAccessResult> RequireOwnerAsync(
      Guid characterId,
      CancellationToken cancellationToken = default)
  {
    var access = await RequireAccessAsync(characterId, cancellationToken);

    if (!access.IsOwner)
    {
      throw new ForbiddenException(
          "Only the character's owner can do this.");
    }

    return access;
  }

  // A missing character and one the user cannot see look the same, so
  // callers cannot probe which ids exist.
  private async Task<CharacterAccessResult> RequireAccessAsync(
      Guid characterId,
      CancellationToken cancellationToken)
  {
    return await FindAccessAsync(characterId, cancellationToken)
        ?? throw new NotFoundException("Character not found.");
  }

  // One query: the character (tracked) plus the current user's role in its
  // setting. The setting owner always counts as a GameMaster member, as in
  // SettingAccess.
  private async Task<CharacterAccessResult?> FindAccessAsync(
      Guid characterId,
      CancellationToken cancellationToken)
  {
    var userId = _currentUser.UserId;

    var row = await _db.Characters
        .Where(character => character.Id == characterId)
        .Select(character => new
        {
          Character = character,
          IsOwner = character.OwnerUserId == userId,
          IsSettingOwner = character.CampaignSetting.OwnerUserId == userId,
          Role = character.CampaignSetting.Memberships
              .Where(membership => membership.UserId == userId)
              .Select(membership => (SettingRole?)membership.Role)
              .FirstOrDefault(),
        })
        .SingleOrDefaultAsync(cancellationToken);

    if (row is null)
    {
      return null;
    }

    var role = row.IsSettingOwner ? SettingRole.GameMaster : row.Role;

    if (row.IsOwner)
    {
      return new CharacterAccessResult(row.Character, true, IsReadOnly: role is null);
    }

    return role == SettingRole.GameMaster
        ? new CharacterAccessResult(row.Character, false, IsReadOnly: true)
        : null;
  }
}
