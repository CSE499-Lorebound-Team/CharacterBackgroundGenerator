using System.Net;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Dashboard;

// P8-01: GET /api/dashboard, the dashboard page in one request.
[Collection(PostgresCollection.Name)]
public class DashboardTests : PostgresTestBase
{
  public DashboardTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static async Task<JsonElement> GetAsync(SharingWorld world, Caller caller)
  {
    var response = await world[caller].GetAsync("/api/dashboard");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static List<string> Names(JsonElement dashboard, string list) =>
      dashboard.GetProperty(list).EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToList();

  private static List<string> ActivityTexts(JsonElement dashboard) =>
      dashboard.GetProperty("recentActivity").EnumerateArray().Select(i => i.GetProperty("text").GetString()!).ToList();

  private static int Count(JsonElement dashboard, string name) =>
      dashboard.GetProperty("counts").GetProperty(name).GetInt32();

  // SaveChanges stamps UpdatedAt with the current time; tests that depend on
  // the order (or on "edited" wording) set it explicitly.
  private Task SetSettingUpdatedAtAsync(Guid settingId, int minutesAgo) =>
      Factory.WithDbAsync(db => db.CampaignSettings
          .Where(s => s.Id == settingId)
          .ExecuteUpdateAsync(set => set.SetProperty(s => s.UpdatedAt, DateTimeOffset.UtcNow.AddMinutes(-minutesAgo))));

  private Task SetEntryUpdatedAtAsync(SettingEntry entry, int minutesAgo) =>
      Factory.WithDbAsync(db => db.SettingEntries
          .Where(e => e.Id == entry.Id)
          .ExecuteUpdateAsync(set => set.SetProperty(e => e.UpdatedAt, DateTimeOffset.UtcNow.AddMinutes(-minutesAgo))));

  private Task SetCharacterUpdatedAtAsync(Character character, int minutesAgo) =>
      Factory.WithDbAsync(db => db.Characters
          .Where(c => c.Id == character.Id)
          .ExecuteUpdateAsync(set => set.SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow.AddMinutes(-minutesAgo))));

  // Moves CreatedAt back so the row reads as edited after it was created.
  private Task AgeSettingAsync(Guid settingId) =>
      Factory.WithDbAsync(db => db.CampaignSettings
          .Where(s => s.Id == settingId)
          .ExecuteUpdateAsync(set => set.SetProperty(s => s.CreatedAt, DateTimeOffset.UtcNow.AddDays(-1))));

  [Fact]
  public async Task Anonymous_callers_get_401()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[Caller.Anonymous].GetAsync("/api/dashboard");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task The_response_has_exactly_the_documented_fields()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddCharacterAsync(Caller.Player);

    var dashboard = await GetAsync(world, Caller.Player);

    Assert.Equal(
        new[] { "counts", "recentActivity", "recentCharacters", "recentSettings" },
        dashboard.EnumerateObject().Select(p => p.Name).Order());
    Assert.Equal(
        new[] { "charactersComplete", "charactersDraft", "settingsAsGm", "settingsAsPlayer" },
        dashboard.GetProperty("counts").EnumerateObject().Select(p => p.Name).Order());
    var activity = dashboard.GetProperty("recentActivity")[0];
    Assert.Equal(
        new[] { "at", "id", "kind", "settingId", "text" },
        activity.EnumerateObject().Select(p => p.Name).Order());
  }

  [Fact]
  public async Task Counts_follow_the_callers_roles_and_character_statuses()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.CreateSettingAsync("Eberron");
    await world.AddCharacterAsync(Caller.Player, "Draft one");
    await world.AddCharacterAsync(Caller.Player, "Draft two");
    await world.AddCharacterAsync(Caller.Player, "Done", c => c.Status = CharacterStatus.Complete);
    await world.AddCharacterAsync(Caller.GameMaster, "The GM's");

    var owner = await GetAsync(world, Caller.Owner);
    var gameMaster = await GetAsync(world, Caller.GameMaster);
    var player = await GetAsync(world, Caller.Player);
    var nonMember = await GetAsync(world, Caller.NonMember);

    Assert.Equal((2, 0, 0, 0), Counts(owner));
    Assert.Equal((1, 0, 1, 0), Counts(gameMaster));
    Assert.Equal((0, 1, 2, 1), Counts(player));
    Assert.Equal((0, 0, 0, 0), Counts(nonMember));

    static (int, int, int, int) Counts(JsonElement d) => (
        Count(d, "settingsAsGm"),
        Count(d, "settingsAsPlayer"),
        Count(d, "charactersDraft"),
        Count(d, "charactersComplete"));
  }

  [Fact]
  public async Task The_owner_counts_as_GameMaster_without_a_membership_row()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.RemoveMemberAsync(Caller.Owner);

    var dashboard = await GetAsync(world, Caller.Owner);

    Assert.Equal(1, Count(dashboard, "settingsAsGm"));
    Assert.Equal(["Osepia"], Names(dashboard, "recentSettings"));
  }

  [Fact]
  public async Task Recent_settings_are_the_five_most_recently_updated_and_match_the_settings_list()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await SetSettingUpdatedAtAsync(world.SettingId, 100);
    var names = new[] { "Two", "Three", "Four", "Five", "Six", "Seven" };
    for (var i = 0; i < names.Length; i++)
    {
      await SetSettingUpdatedAtAsync(await world.CreateSettingAsync(names[i]), 60 - i);
    }

    var dashboard = await GetAsync(world, Caller.Owner);

    Assert.Equal(["Seven", "Six", "Five", "Four", "Three"], Names(dashboard, "recentSettings"));
    var list = await JsonAssert.ReadJsonAsync(await world[Caller.Owner].GetAsync("/api/settings?pageSize=5"));
    Assert.Equal(
        list.GetProperty("items").GetRawText(),
        dashboard.GetProperty("recentSettings").GetRawText());
  }

  [Fact]
  public async Task Recent_settings_hide_GM_only_entries_from_a_Players_entry_count()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sasymon");
    await world.AddEntryAsync("Cult of Belena", SettingEntryType.Faction, isGmOnly: true);

    var player = await GetAsync(world, Caller.Player);
    var gameMaster = await GetAsync(world, Caller.GameMaster);

    Assert.Equal(1, player.GetProperty("recentSettings")[0].GetProperty("entryCount").GetInt32());
    Assert.Equal(2, gameMaster.GetProperty("recentSettings")[0].GetProperty("entryCount").GetInt32());
  }

  [Fact]
  public async Task Recent_characters_are_the_callers_five_most_recently_updated_and_match_the_characters_list()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var names = new[] { "One", "Two", "Three", "Four", "Five", "Six" };
    for (var i = 0; i < names.Length; i++)
    {
      await SetCharacterUpdatedAtAsync(await world.AddCharacterAsync(Caller.Player, names[i]), 60 - i);
    }
    await world.AddCharacterAsync(Caller.GameMaster, "The GM's");

    var dashboard = await GetAsync(world, Caller.Player);

    Assert.Equal(["Six", "Five", "Four", "Three", "Two"], Names(dashboard, "recentCharacters"));
    var list = await JsonAssert.ReadJsonAsync(await world[Caller.Player].GetAsync("/api/characters?pageSize=5"));
    Assert.Equal(
        list.GetProperty("items").GetRawText(),
        dashboard.GetProperty("recentCharacters").GetRawText());
  }

  [Fact]
  public async Task Activity_merges_settings_entries_and_characters_newest_first()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await SetSettingUpdatedAtAsync(world.SettingId, 30);
    var entry = await world.AddEntryAsync("Sasymon");
    await SetEntryUpdatedAtAsync(entry, 20);
    var character = await world.AddCharacterAsync(Caller.Player, "Theron Vale");
    await SetCharacterUpdatedAtAsync(character, 10);

    var dashboard = await GetAsync(world, Caller.Player);

    var items = dashboard.GetProperty("recentActivity").EnumerateArray().ToList();
    Assert.Equal(
        new[] { "Character", "Entry", "Setting" },
        items.Select(i => i.GetProperty("kind").GetString()));
    Assert.Equal(
        new[] { character.Id, entry.Id, world.SettingId },
        items.Select(i => i.GetProperty("id").GetGuid()));
    Assert.All(items, i => Assert.Equal(world.SettingId, i.GetProperty("settingId").GetGuid()));
  }

  [Fact]
  public async Task Activity_text_says_whether_the_item_was_created_or_edited()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sasymon");
    await world.AddCharacterAsync(Caller.Player, "Theron Vale");

    var created = ActivityTexts(await GetAsync(world, Caller.Player));

    Assert.Contains("Created Osepia", created);
    Assert.Contains("Added Sasymon to Osepia", created);
    Assert.Contains("Started Theron Vale", created);

    await AgeSettingAsync(world.SettingId);
    var entry = await world.AddEntryAsync("Nigallu");
    var character = await world.AddCharacterAsync(Caller.Player, "Aster");
    await Factory.WithDbAsync(async db =>
    {
      await db.SettingEntries.Where(e => e.Id == entry.Id)
          .ExecuteUpdateAsync(set => set.SetProperty(e => e.UpdatedAt, e => e.CreatedAt.AddMinutes(1)));
      return await db.Characters.Where(c => c.Id == character.Id)
          .ExecuteUpdateAsync(set => set.SetProperty(c => c.UpdatedAt, c => c.CreatedAt.AddMinutes(1)));
    });

    var edited = ActivityTexts(await GetAsync(world, Caller.Player));

    Assert.Contains("Edited Osepia", edited);
    Assert.Contains("Edited Nigallu", edited);
    Assert.Contains("Updated Aster", edited);
  }

  [Fact]
  public async Task Activity_is_capped_at_ten()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    for (var i = 0; i < 12; i++)
    {
      await world.AddEntryAsync($"Entry {i}");
    }

    var dashboard = await GetAsync(world, Caller.Player);

    Assert.Equal(10, dashboard.GetProperty("recentActivity").GetArrayLength());
  }

  [Fact]
  public async Task Players_never_see_activity_on_GM_only_entries()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sasymon");
    await world.AddEntryAsync("Cult of Belena", SettingEntryType.Faction, isGmOnly: true);

    var player = ActivityTexts(await GetAsync(world, Caller.Player));
    var gameMaster = ActivityTexts(await GetAsync(world, Caller.GameMaster));
    var owner = ActivityTexts(await GetAsync(world, Caller.Owner));

    Assert.Contains("Added Sasymon to Osepia", player);
    Assert.DoesNotContain(player, text => text.Contains("Cult of Belena"));
    Assert.Contains("Added Cult of Belena to Osepia", gameMaster);
    Assert.Contains("Added Cult of Belena to Osepia", owner);
  }

  [Fact]
  public async Task Activity_leaves_out_other_settings_and_other_users_characters()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var other = await world.CreateSettingAsync("Eberron");
    await world.AddEntryAsync("Sharn", settingId: other);
    await world.AddCharacterAsync(Caller.GameMaster, "The GM's");

    var player = ActivityTexts(await GetAsync(world, Caller.Player));
    var nonMember = await GetAsync(world, Caller.NonMember);

    Assert.DoesNotContain(player, text => text.Contains("Eberron") || text.Contains("Sharn"));
    Assert.DoesNotContain(player, text => text.Contains("The GM's"));
    Assert.Equal(0, nonMember.GetProperty("recentActivity").GetArrayLength());
    Assert.Empty(Names(nonMember, "recentSettings"));
    Assert.Empty(Names(nonMember, "recentCharacters"));
  }

  [Fact]
  public async Task A_removed_players_characters_stay_but_the_settings_activity_goes()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sasymon");
    await world.AddCharacterAsync(Caller.Player, "Theron Vale");

    await world.RemoveMemberAsync(Caller.Player);
    var dashboard = await GetAsync(world, Caller.Player);

    Assert.Equal(["Started Theron Vale"], ActivityTexts(dashboard));
    Assert.True(dashboard.GetProperty("recentCharacters")[0].GetProperty("isReadOnly").GetBoolean());
    Assert.Equal(0, Count(dashboard, "settingsAsPlayer"));
  }
}
