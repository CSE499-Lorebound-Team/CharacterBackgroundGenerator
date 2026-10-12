using Lorebound.Api.Models;

namespace Lorebound.Api.Auth;

/// <summary>
/// The one place that decides what the current user may do with a character
/// (P6-02). Every character endpoint calls it first.
/// <list type="bullet">
/// <item>The owner, while still a member of the setting: read and write.</item>
/// <item>The owner after removal from the setting: read and delete only
/// (<see cref="CharacterAccessResult.IsReadOnly"/>). Re-joining restores
/// write access, since membership is checked on every call.</item>
/// <item>A GameMaster (or the owner) of the character's setting: read only.</item>
/// <item>Anyone else: <see cref="Errors.NotFoundException"/> (404), the same
/// as a missing character.</item>
/// </list>
/// A caller who can read but not do the requested thing gets
/// <see cref="Errors.ForbiddenException"/> (403).
/// </summary>
public interface ICharacterAccess
{
  /// <summary>The owner (even read-only) or a GameMaster of the setting.</summary>
  Task<CharacterAccessResult> RequireReadAsync(
      Guid characterId,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// The owner while still a member; a read-only owner or a GameMaster
  /// gets 403.
  /// </summary>
  Task<CharacterAccessResult> RequireWriteAsync(
      Guid characterId,
      CancellationToken cancellationToken = default);

  /// <summary>The owner, even read-only (for delete); a GameMaster gets 403.</summary>
  Task<CharacterAccessResult> RequireOwnerAsync(
      Guid characterId,
      CancellationToken cancellationToken = default);
}

/// <param name="Character">Tracked, so callers can edit and save it.</param>
/// <param name="IsOwner">The current user owns the character.</param>
/// <param name="IsReadOnly">
/// The current user cannot edit it: the owner was removed from the setting,
/// or the caller is a GameMaster viewing someone else's character.
/// </param>
public sealed record CharacterAccessResult(
    Character Character,
    bool IsOwner,
    bool IsReadOnly);
