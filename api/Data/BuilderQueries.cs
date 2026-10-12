using Lorebound.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Data;

/// <summary>One option for a builder step: an entry of the step's type.</summary>
public sealed record BuilderOption(Guid EntryId, string Name, string? Description);

/// <summary>
/// A step's options and whether earlier choices narrowed them (P7-02,
/// ADR 0002).
/// </summary>
public sealed record BuilderOptions(bool Narrowed, IReadOnlyList<BuilderOption> Options);

public static class BuilderQueries
{
  /// <summary>
  /// The options for <paramref name="step"/> of a character in
  /// <paramref name="settingId"/>, narrowed by the entries chosen in earlier
  /// steps (ADR 0002):
  /// <list type="bullet">
  /// <item>Candidates are the setting's entries of the step's type that
  /// <paramref name="role"/> may see.</item>
  /// <item>A candidate is linked when any relationship, of any type and in
  /// either direction, joins it to an entry chosen in an earlier step.
  /// Relationships <paramref name="role"/> may not see do not count, so
  /// narrowing never hints at hidden lore.</item>
  /// <item>At least one linked candidate: only those, narrowed. Otherwise
  /// every candidate, not narrowed, so the builder never dead-ends.</item>
  /// </list>
  /// A step that takes no entries has no options. Sorted by name.
  /// </summary>
  public static async Task<BuilderOptions> LoadOptionsAsync(
      this LoreboundDbContext db,
      Guid characterId,
      Guid settingId,
      BuilderStep step,
      SettingRole role,
      CancellationToken cancellationToken = default)
  {
    if (step.EntryType is not { } entryType)
    {
      return new BuilderOptions(false, []);
    }

    var earlierKeys = BuilderSteps.All
        .Where(other => other.Order < step.Order && other.EntryType is not null)
        .Select(other => other.Key)
        .ToList();

    var chosen = db.CharacterChoices
        .Where(choice => choice.CharacterId == characterId
            && earlierKeys.Contains(choice.StepKey)
            && choice.EntryId != null)
        .Select(choice => choice.EntryId!.Value);

    var candidates = db.SettingEntries
        .AsNoTracking()
        .Where(entry => entry.CampaignSettingId == settingId && entry.EntryType == entryType)
        .VisibleTo(role);

    var links = db.SettingEntryRelationships
        .Where(link => link.CampaignSettingId == settingId)
        .VisibleTo(role);

    var linked = candidates.Where(entry => links.Any(link =>
        (link.SourceEntryId == entry.Id && chosen.Contains(link.TargetEntryId))
        || (link.TargetEntryId == entry.Id && chosen.Contains(link.SourceEntryId))));

    var options = await ToOptionsAsync(linked, cancellationToken);
    if (options.Count > 0)
    {
      return new BuilderOptions(true, options);
    }

    return new BuilderOptions(false, await ToOptionsAsync(candidates, cancellationToken));
  }

  /// <summary>
  /// The keys, in step order, of a character's stale steps (P7-04): entry
  /// steps whose options are narrowed by earlier choices but no longer
  /// include an entry chosen for the step, typically after an earlier step
  /// changed. Computed for <paramref name="role"/>, the owner's role, so a
  /// Player's character is judged by what the Player may see. A choice whose
  /// entry was deleted is not stale (P7-05 reports it as missing).
  /// </summary>
  public static async Task<IReadOnlyList<string>> LoadStaleStepsAsync(
      this LoreboundDbContext db,
      Guid characterId,
      Guid settingId,
      SettingRole role,
      CancellationToken cancellationToken = default)
  {
    var chosen = await db.CharacterChoices
        .AsNoTracking()
        .Where(choice => choice.CharacterId == characterId && choice.EntryId != null)
        .Select(choice => new { choice.StepKey, EntryId = choice.EntryId!.Value })
        .ToListAsync(cancellationToken);

    var stale = new List<string>();
    foreach (var step in BuilderSteps.All.Where(step => step.EntryType is not null))
    {
      var entryIds = chosen.Where(choice => choice.StepKey == step.Key).Select(choice => choice.EntryId).ToList();
      if (entryIds.Count == 0)
      {
        continue;
      }

      var options = await db.LoadOptionsAsync(characterId, settingId, step, role, cancellationToken);
      if (options.Narrowed && entryIds.Any(id => options.Options.All(option => option.EntryId != id)))
      {
        stale.Add(step.Key);
      }
    }

    return stale;
  }

  /// <summary>
  /// The role the character's owner holds in its setting: the setting owner
  /// is a GameMaster; an owner removed from the setting is judged as a
  /// Player, so a read-only character never reveals hidden lore.
  /// </summary>
  public static async Task<SettingRole> LoadOwnerRoleAsync(
      this LoreboundDbContext db,
      Guid settingId,
      Guid ownerUserId,
      CancellationToken cancellationToken = default)
  {
    var row = await db.CampaignSettings
        .AsNoTracking()
        .Where(setting => setting.Id == settingId)
        .Select(setting => new
        {
          IsSettingOwner = setting.OwnerUserId == ownerUserId,
          Role = setting.Memberships
              .Where(membership => membership.UserId == ownerUserId)
              .Select(membership => (SettingRole?)membership.Role)
              .FirstOrDefault(),
        })
        .SingleOrDefaultAsync(cancellationToken);

    return row is { IsSettingOwner: true } ? SettingRole.GameMaster : row?.Role ?? SettingRole.Player;
  }

  private static Task<List<BuilderOption>> ToOptionsAsync(
      IQueryable<SettingEntry> entries,
      CancellationToken cancellationToken) =>
      entries
          .OrderBy(entry => entry.Name)
          .ThenBy(entry => entry.Id)
          .Select(entry => new BuilderOption(entry.Id, entry.Name, entry.Description))
          .ToListAsync(cancellationToken);
}
