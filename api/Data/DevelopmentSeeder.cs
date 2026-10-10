using Lorebound.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Data;

/// <summary>
/// Development-only sample data (P8-02), so a fresh clone looks like the
/// wireframes: a GameMaster who owns "Osepia" with linked entries (one
/// GM-only), and a Player member with a draft character. Idempotent: every
/// row is looked up by its natural key first, so running it again adds only
/// what is missing and never changes or resets existing rows (passwords
/// included). Run through <see cref="DevelopmentSeedingSetup"/>.
/// </summary>
public class DevelopmentSeeder
{
  // Development-only credentials, documented in the README. Startup refuses
  // to seed outside Development, so these never reach Production.
  public const string GmEmail = "gm@lorebound.local";
  public const string GmPassword = "lorebound-gm-dev";
  public const string GmDisplayName = "Joseph Marlow";

  public const string PlayerEmail = "player@lorebound.local";
  public const string PlayerPassword = "lorebound-player-dev";
  public const string PlayerDisplayName = "Lyra Holt";

  public const string SettingName = "Osepia";
  public const string CharacterName = "Theron Vale";

  private const string SettingDescription =
      "A large campaign setting filled with competing nations, cultures, and ancient history.";

  private static readonly (string Name, SettingEntryType Type, bool IsGmOnly, string Description)[] Entries =
  [
    ("Sasymon", SettingEntryType.Location, false,
        "A river city of bridges and toll gates, rich from the trade that passes through it."),
    ("Ymenite Region", SettingEntryType.Location, false,
        "Fertile lowlands along the great river, dotted with walled market towns."),
    ("Northern Marches", SettingEntryType.Location, false,
        "Cold border hills held by watchtowers and stubborn clans."),
    ("River Cities", SettingEntryType.Culture, false,
        "Merchants and boatmen who trust contracts over oaths."),
    ("Nigallu", SettingEntryType.Culture, false,
        "Hill clans of the north, keepers of long memories and longer feuds."),
    ("Merchant Guild", SettingEntryType.Organization, false,
        "The guild that sets the tolls, hires the guards and settles every trade dispute."),
    ("Caravan Guard", SettingEntryType.Profession, false,
        "Sellswords who keep the trade roads open, for a share of the cargo."),
    ("Scholar", SettingEntryType.Profession, false,
        "Readers of old ledgers and older ruins."),
    ("Cult of Beléna", SettingEntryType.Religion, true,
        "A hidden faith spreading through the Marches. Its true aims are known only to the GM."),
  ];

  private static readonly (string Source, string Target, string Type)[] Relationships =
  [
    ("Sasymon", "Ymenite Region", "Located in"),
    ("River Cities", "Sasymon", "Native to"),
    ("Nigallu", "Northern Marches", "Native to"),
    ("Merchant Guild", "Sasymon", "Located in"),
    ("Caravan Guard", "Merchant Guild", "Member of"),
    ("Northern Marches", "Ymenite Region", "Borders"),
    // Links a GM-only entry, so Players never see it (P5-02).
    ("Cult of Beléna", "Northern Marches", "Practiced in"),
  ];

  private readonly LoreboundDbContext _db;
  private readonly UserManager<ApplicationUser> _userManager;
  private readonly ILogger<DevelopmentSeeder> _logger;

  public DevelopmentSeeder(
      LoreboundDbContext db,
      UserManager<ApplicationUser> userManager,
      ILogger<DevelopmentSeeder> logger)
  {
    _db = db;
    _userManager = userManager;
    _logger = logger;
  }

  public async Task SeedAsync(CancellationToken cancellationToken = default)
  {
    // Checked first, so an out-of-date database fails with the fix rather
    // than a missing-table error halfway through.
    var pending = (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
    if (pending.Count > 0)
    {
      throw new InvalidOperationException(
          $"The database is missing {pending.Count} migration(s) ({string.Join(", ", pending)}). " +
          "Run `dotnet ef database update` from api/ (or scripts/onboard.ps1), then seed again.");
    }

    var gm =await EnsureUserAsync(GmEmail, GmDisplayName, GmPassword);
    var player = await EnsureUserAsync(PlayerEmail, PlayerDisplayName, PlayerPassword);

    var setting = await _db.CampaignSettings
        .SingleOrDefaultAsync(s => s.OwnerUserId == gm.Id && s.Name == SettingName, cancellationToken);
    if (setting is null)
    {
      setting = new CampaignSetting
      {
        Id = Guid.NewGuid(),
        Name = SettingName,
        Description = SettingDescription,
        OwnerUserId = gm.Id,
      };
      _db.CampaignSettings.Add(setting);
    }

    await EnsureMembershipAsync(setting.Id, gm.Id, SettingRole.GameMaster, cancellationToken);
    await EnsureMembershipAsync(setting.Id, player.Id, SettingRole.Player, cancellationToken);
    await _db.SaveChangesAsync(cancellationToken);

    var existing = await _db.SettingEntries
        .Where(e => e.CampaignSettingId == setting.Id)
        .ToListAsync(cancellationToken);
    var entries = new Dictionary<string, SettingEntry>();
    foreach (var (name, type, isGmOnly, description) in Entries)
    {
      var entry = existing.FirstOrDefault(e =>
          e.EntryType == type && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
      if (entry is null)
      {
        entry = new SettingEntry
        {
          Id = Guid.NewGuid(),
          CampaignSettingId = setting.Id,
          Name = name,
          EntryType = type,
          IsGmOnly = isGmOnly,
          Description = description,
        };
        _db.SettingEntries.Add(entry);
      }

      entries[name] = entry;
    }
    await _db.SaveChangesAsync(cancellationToken);

    var links = await _db.SettingEntryRelationships
        .Where(r => r.CampaignSettingId == setting.Id)
        .Select(r => new { r.SourceEntryId, r.TargetEntryId, r.RelationshipType })
        .ToListAsync(cancellationToken);
    foreach (var (source, target, type) in Relationships)
    {
      var sourceId = entries[source].Id;
      var targetId = entries[target].Id;
      if (!links.Any(l => l.SourceEntryId == sourceId && l.TargetEntryId == targetId
          && string.Equals(l.RelationshipType, type, StringComparison.OrdinalIgnoreCase)))
      {
        _db.SettingEntryRelationships.Add(new SettingEntryRelationship
        {
          Id = Guid.NewGuid(),
          CampaignSettingId = setting.Id,
          SourceEntryId = sourceId,
          TargetEntryId = targetId,
          RelationshipType = type,
        });
      }
    }

    // A draft on the culture step, with its homeland chosen, so the
    // Characters page and dashboard show a homeland.
    var hasCharacter = await _db.Characters.AnyAsync(
        c => c.OwnerUserId == player.Id && c.CampaignSettingId == setting.Id && c.Name == CharacterName,
        cancellationToken);
    if (!hasCharacter)
    {
      var character = new Character
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = setting.Id,
        OwnerUserId = player.Id,
        Name = CharacterName,
        Status = CharacterStatus.Draft,
        CurrentStep = 3,
      };
      character.Choices.Add(new CharacterChoice
      {
        Id = Guid.NewGuid(),
        StepKey = CharacterStepKeys.Homeland,
        Ordinal = 0,
        EntryId = entries["Sasymon"].Id,
      });
      _db.Characters.Add(character);
    }

    await _db.SaveChangesAsync(cancellationToken);

    _logger.LogInformation(
        "Development seed data is in place: sign in as {GmEmail} (GameMaster) or {PlayerEmail} (Player).",
        GmEmail,
        PlayerEmail);
  }

  private async Task<ApplicationUser> EnsureUserAsync(string email, string displayName, string password)
  {
    var user = await _userManager.FindByEmailAsync(email);
    if (user is not null)
    {
      return user;
    }

    user = new ApplicationUser
    {
      UserName = email,
      Email = email,
      EmailConfirmed = true,
      DisplayName = displayName,
    };

    var result = await _userManager.CreateAsync(user, password);
    if (!result.Succeeded)
    {
      throw new InvalidOperationException(
          $"Could not create the seed user {email}: " +
          string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    return user;
  }

  private async Task EnsureMembershipAsync(
      Guid settingId,
      Guid userId,
      SettingRole role,
      CancellationToken cancellationToken)
  {
    var exists = _db.SettingMemberships.Local.Any(m => m.CampaignSettingId == settingId && m.UserId == userId)
        || await _db.SettingMemberships.AnyAsync(
            m => m.CampaignSettingId == settingId && m.UserId == userId, cancellationToken);
    if (!exists)
    {
      _db.SettingMemberships.Add(new SettingMembership
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = settingId,
        UserId = userId,
        Role = role,
      });
    }
  }
}
