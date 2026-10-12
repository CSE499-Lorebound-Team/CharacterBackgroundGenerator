using Lorebound.Api.Data;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lorebound.Api.Tests.Data;

// P4-01: entry names are citext and unique per (setting, type); IsGmOnly
// defaults to false; VisibleTo hides secret entries from Players.
[Collection(PostgresCollection.Name)]
public class SettingEntryTests : PostgresTestBase
{
  public SettingEntryTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private async Task<CampaignSetting> CreateSettingAsync(string name = "Eberron")
  {
    var owner = await Factory.CreateUserAsync($"{name}@example.com".ToLowerInvariant(), "Owner");

    var setting = new CampaignSetting
    {
      Id = Guid.NewGuid(),
      Name = name,
      OwnerUserId = owner.Id,
    };

    await Factory.WithDbAsync(db =>
    {
      db.CampaignSettings.Add(setting);
      return db.SaveChangesAsync();
    });

    return setting;
  }

  private static SettingEntry Entry(
      Guid settingId,
      string name,
      SettingEntryType type = SettingEntryType.Location,
      bool isGmOnly = false) =>
      new()
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = settingId,
        Name = name,
        EntryType = type,
        IsGmOnly = isGmOnly,
      };

  private Task<int> AddAsync(params SettingEntry[] entries) =>
      Factory.WithDbAsync(db =>
      {
        db.SettingEntries.AddRange(entries);
        return db.SaveChangesAsync();
      });

  [Fact]
  public async Task Names_differing_only_by_case_violate_the_unique_index()
  {
    var setting = await CreateSettingAsync();
    await AddAsync(Entry(setting.Id, "Sharn"));

    var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
        AddAsync(Entry(setting.Id, "SHARN")));

    var postgres = Assert.IsType<PostgresException>(error.InnerException);
    Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    Assert.Equal("IX_SettingEntries_CampaignSettingId_EntryType_Name", postgres.ConstraintName);
  }

  [Fact]
  public async Task The_same_name_is_allowed_in_another_type_or_setting()
  {
    var eberron = await CreateSettingAsync("Eberron");
    var krynn = await CreateSettingAsync("Krynn");

    await AddAsync(
        Entry(eberron.Id, "Sharn", SettingEntryType.Location),
        Entry(eberron.Id, "sharn", SettingEntryType.Faction),
        Entry(krynn.Id, "SHARN", SettingEntryType.Location));

    Assert.Equal(3, await Factory.WithDbAsync(db => db.SettingEntries.CountAsync()));
  }

  [Fact]
  public async Task Lookups_by_name_ignore_case()
  {
    var setting = await CreateSettingAsync();
    await AddAsync(Entry(setting.Id, "Sharn"));

    var found = await Factory.WithDbAsync(db =>
        db.SettingEntries.AnyAsync(entry => entry.Name == "sHaRn"));

    Assert.True(found);
  }

  [Fact]
  public async Task IsGmOnly_defaults_to_false()
  {
    var setting = await CreateSettingAsync();
    await AddAsync(Entry(setting.Id, "Sharn"));

    var saved = await Factory.WithDbAsync(db => db.SettingEntries.SingleAsync());

    Assert.False(saved.IsGmOnly);
  }

  [Fact]
  public async Task VisibleTo_hides_GM_only_entries_from_players_only()
  {
    var setting = await CreateSettingAsync();
    await AddAsync(
        Entry(setting.Id, "Sharn"),
        Entry(setting.Id, "The Lord of Blades", SettingEntryType.Person, isGmOnly: true));

    var forPlayer = await Factory.WithDbAsync(db => db.SettingEntries
        .VisibleTo(SettingRole.Player)
        .Select(entry => entry.Name)
        .ToListAsync());
    var forGm = await Factory.WithDbAsync(db => db.SettingEntries
        .VisibleTo(SettingRole.GameMaster)
        .Select(entry => entry.Name)
        .OrderBy(name => name)
        .ToListAsync());

    Assert.Equal(["Sharn"], forPlayer);
    Assert.Equal(["Sharn", "The Lord of Blades"], forGm);
  }
}
