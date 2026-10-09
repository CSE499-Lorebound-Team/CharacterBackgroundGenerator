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

  private static Task<List<BuilderOption>> ToOptionsAsync(
      IQueryable<SettingEntry> entries,
      CancellationToken cancellationToken) =>
      entries
          .OrderBy(entry => entry.Name)
          .ThenBy(entry => entry.Id)
          .Select(entry => new BuilderOption(entry.Id, entry.Name, entry.Description))
          .ToListAsync(cancellationToken);
}
