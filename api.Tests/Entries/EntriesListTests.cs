using System.Net;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Entries;

// P4-02: GET /api/settings/{sid}/entries.
[Collection(PostgresCollection.Name)]
public class EntriesListTests : PostgresTestBase
{
  public EntriesListTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static async Task<JsonElement> ListAsync(SharingWorld world, Caller caller, string query = "")
  {
    var response = await world[caller].GetAsync($"/api/settings/{world.SettingId}/entries{query}");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static List<string> Names(JsonElement page) =>
      page.GetProperty("items").EnumerateArray()
          .Select(item => item.GetProperty("name").GetString()!)
          .ToList();

  [Fact]
  public async Task Lists_entries_by_name_with_the_documented_shape()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sharn", description: "City of Towers");
    await world.AddEntryAsync("Aundair", SettingEntryType.Location);

    var page = await ListAsync(world, Caller.GameMaster);

    Assert.Equal(["Aundair", "Sharn"], Names(page));
    Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
    var item = page.GetProperty("items")[1];
    Assert.Equal(
        new[] { "description", "entryType", "id", "isGmOnly", "name", "relationshipCount", "updatedAt" },
        item.EnumerateObject().Select(p => p.Name).Order());
    Assert.Equal("Location", item.GetProperty("entryType").GetString());
    Assert.Equal("City of Towers", item.GetProperty("description").GetString());
    Assert.False(item.GetProperty("isGmOnly").GetBoolean());
  }

  [Theory]
  [InlineData(Caller.Owner)]
  [InlineData(Caller.GameMaster)]
  public async Task GameMasters_see_GM_only_entries_flagged(Caller caller)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sharn");
    await world.AddEntryAsync("The Lord of Blades", SettingEntryType.Person, isGmOnly: true);

    var page = await ListAsync(world, caller);

    Assert.Equal(["Sharn", "The Lord of Blades"], Names(page));
    Assert.True(page.GetProperty("items")[1].GetProperty("isGmOnly").GetBoolean());
  }

  [Fact]
  public async Task Players_get_neither_GM_only_entries_nor_the_flag()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sharn");
    await world.AddEntryAsync("The Lord of Blades", SettingEntryType.Person, isGmOnly: true);

    var page = await ListAsync(world, Caller.Player);

    Assert.Equal(["Sharn"], Names(page));
    Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
    Assert.False(page.GetProperty("items")[0].TryGetProperty("isGmOnly", out _));
  }

  [Fact]
  public async Task Filters_by_type()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sharn", SettingEntryType.Location);
    await world.AddEntryAsync("Dark Six", SettingEntryType.Religion);

    var page = await ListAsync(world, Caller.Player, "?type=Religion");

    Assert.Equal(["Dark Six"], Names(page));
  }

  [Fact]
  public async Task An_unknown_type_is_400()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[Caller.Player].GetAsync($"/api/settings/{world.SettingId}/entries?type=Planet");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Search_matches_name_or_description_ignoring_case()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sharn", description: "City of Towers");
    await world.AddEntryAsync("Towering Wood", SettingEntryType.Location);
    await world.AddEntryAsync("Aundair", description: "Arcane kingdom");

    var page = await ListAsync(world, Caller.Player, "?search=TOWER");

    Assert.Equal(["Sharn", "Towering Wood"], Names(page));
  }

  [Fact]
  public async Task Search_treats_wildcards_literally()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("100% Iron", SettingEntryType.Other);
    await world.AddEntryAsync("100 Iron", SettingEntryType.Other);
    await world.AddEntryAsync("A_B", SettingEntryType.Other);
    await world.AddEntryAsync("AxB", SettingEntryType.Other);

    Assert.Equal(["100% Iron"], Names(await ListAsync(world, Caller.Player, "?search=0%25")));
    Assert.Equal(["A_B"], Names(await ListAsync(world, Caller.Player, "?search=A_")));
  }

  [Fact]
  public async Task GameMasters_can_filter_by_gmOnly_and_players_find_nothing_secret()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sharn");
    await world.AddEntryAsync("The Lord of Blades", SettingEntryType.Person, isGmOnly: true);

    Assert.Equal(["The Lord of Blades"], Names(await ListAsync(world, Caller.GameMaster, "?gmOnly=true")));
    Assert.Equal(["Sharn"], Names(await ListAsync(world, Caller.GameMaster, "?gmOnly=false")));
    Assert.Empty(Names(await ListAsync(world, Caller.Player, "?gmOnly=true")));
    Assert.Equal(["Sharn"], Names(await ListAsync(world, Caller.Player, "?gmOnly=false")));
  }

  [Fact]
  public async Task Pages_through_the_entries()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    foreach (var name in new[] { "E", "D", "C", "B", "A" })
    {
      await world.AddEntryAsync(name);
    }

    var page = await ListAsync(world, Caller.Player, "?page=2&pageSize=2");

    Assert.Equal(["C", "D"], Names(page));
    Assert.Equal(2, page.GetProperty("page").GetInt32());
    Assert.Equal(2, page.GetProperty("pageSize").GetInt32());
    Assert.Equal(5, page.GetProperty("totalCount").GetInt32());
  }

  [Fact]
  public async Task Only_this_settings_entries_are_listed()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");
    await world.AddEntryAsync("Sharn");
    await world.AddEntryAsync("Palanthas", settingId: krynnId);

    Assert.Equal(["Sharn"], Names(await ListAsync(world, Caller.Owner)));
  }

  [Fact]
  public async Task Relationship_count_covers_both_directions_and_only_visible_ends()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    var boromar = await world.AddEntryAsync("Boromar Clan", SettingEntryType.Faction);
    var lord = await world.AddEntryAsync("The Lord of Blades", SettingEntryType.Person, isGmOnly: true);
    await world.LinkAsync(sharn, breland, "Capital of");
    await world.LinkAsync(boromar, sharn, "Based in");
    await world.LinkAsync(lord, sharn, "Spies on");

    int CountFor(JsonElement page, string name) =>
        page.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name)
            .GetProperty("relationshipCount").GetInt32();

    var forGm = await ListAsync(world, Caller.GameMaster);
    var forPlayer = await ListAsync(world, Caller.Player);

    Assert.Equal(3, CountFor(forGm, "Sharn"));
    Assert.Equal(1, CountFor(forGm, "The Lord of Blades"));
    Assert.Equal(2, CountFor(forPlayer, "Sharn"));
    Assert.Equal(1, CountFor(forPlayer, "Breland"));
  }
}
