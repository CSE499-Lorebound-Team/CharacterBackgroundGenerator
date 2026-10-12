using System.Net;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Relationships;

// P5-02: GET /api/settings/{sid}/relationships.
[Collection(PostgresCollection.Name)]
public class RelationshipsListTests : PostgresTestBase
{
  public RelationshipsListTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> GetAsync(SharingWorld world, Caller caller, string query = "") =>
      world[caller].GetAsync($"/api/settings/{world.SettingId}/relationships{query}");

  private static async Task<JsonElement> ListAsync(SharingWorld world, Caller caller, string query = "")
  {
    var response = await GetAsync(world, caller, query);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  // "Source -type-> Target" for each item, in order.
  private static List<string> Links(JsonElement page) =>
      page.GetProperty("items").EnumerateArray()
          .Select(item =>
              $"{item.GetProperty("source").GetProperty("name").GetString()} " +
              $"-{item.GetProperty("relationshipType").GetString()}-> " +
              $"{item.GetProperty("target").GetProperty("name").GetString()}")
          .ToList();

  [Fact]
  public async Task Lists_relationships_sorted_with_the_documented_shape()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    var boromar = await world.AddEntryAsync("Boromar Clan", SettingEntryType.Faction);
    var capital = await world.LinkAsync(sharn, breland, "Capital of", "Since the Last War");
    await world.LinkAsync(boromar, sharn, "Based in");

    var page = await ListAsync(world, Caller.Player);

    Assert.Equal(["Boromar Clan -Based in-> Sharn", "Sharn -Capital of-> Breland"], Links(page));
    Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
    Assert.Equal(1, page.GetProperty("page").GetInt32());

    var item = page.GetProperty("items")[1];
    Assert.Equal(
        ["id", "source", "target", "relationshipType", "description", "createdAt", "updatedAt"],
        item.EnumerateObject().Select(p => p.Name));
    Assert.Equal(capital.Id, item.GetProperty("id").GetGuid());
    Assert.Equal("Since the Last War", item.GetProperty("description").GetString());

    var target = item.GetProperty("target");
    Assert.Equal(["id", "name", "entryType"], target.EnumerateObject().Select(p => p.Name));
    Assert.Equal(breland.Id, target.GetProperty("id").GetGuid());
    Assert.Equal("Location", target.GetProperty("entryType").GetString());
    Assert.Equal(sharn.Id, item.GetProperty("source").GetProperty("id").GetGuid());
  }

  [Fact]
  public async Task Players_never_see_a_link_with_a_GM_only_end()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    var lord = await world.AddEntryAsync("The Lord of Blades", SettingEntryType.Person, isGmOnly: true);
    await world.LinkAsync(sharn, breland, "Capital of");
    await world.LinkAsync(lord, sharn, "Spies on");
    await world.LinkAsync(sharn, lord, "Unknowingly hosts");

    var forPlayer = await ListAsync(world, Caller.Player);
    var forGm = await ListAsync(world, Caller.GameMaster);

    Assert.Equal(["Sharn -Capital of-> Breland"], Links(forPlayer));
    Assert.Equal(1, forPlayer.GetProperty("totalCount").GetInt32());
    Assert.DoesNotContain("Lord of Blades", forPlayer.ToString());
    Assert.Equal(3, forGm.GetProperty("totalCount").GetInt32());
  }

  [Fact]
  public async Task EntryId_keeps_incoming_and_outgoing_links_of_that_entry()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    var boromar = await world.AddEntryAsync("Boromar Clan", SettingEntryType.Faction);
    var aundair = await world.AddEntryAsync("Aundair");
    await world.LinkAsync(sharn, breland, "Capital of");
    await world.LinkAsync(boromar, sharn, "Based in");
    await world.LinkAsync(aundair, breland, "Borders");

    var page = await ListAsync(world, Caller.Player, $"?entryId={sharn.Id}");

    Assert.Equal(["Boromar Clan -Based in-> Sharn", "Sharn -Capital of-> Breland"], Links(page));
  }

  [Fact]
  public async Task EntryId_of_a_hidden_missing_or_other_setting_entry_is_the_same_404_for_a_player()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var lord = await world.AddEntryAsync("The Lord of Blades", SettingEntryType.Person, isGmOnly: true);
    var krynnId = await world.CreateSettingAsync("Krynn");
    var elsewhere = await world.AddEntryAsync("Palanthas", settingId: krynnId);

    var details = new List<string?>();
    foreach (var id in new[] { lord.Id, Guid.NewGuid(), elsewhere.Id })
    {
      var response = await GetAsync(world, Caller.Player, $"?entryId={id}");
      Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
      details.Add((await JsonAssert.ReadProblemAsync(response)).GetProperty("detail").GetString());
    }

    Assert.All(details, detail => Assert.Equal("Entry not found.", detail));

    var forGm = await GetAsync(world, Caller.GameMaster, $"?entryId={lord.Id}");
    Assert.Equal(HttpStatusCode.OK, forGm.StatusCode);
  }

  [Fact]
  public async Task Type_matches_the_whole_type_ignoring_case()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    var boromar = await world.AddEntryAsync("Boromar Clan", SettingEntryType.Faction);
    await world.LinkAsync(sharn, breland, "Located in");
    await world.LinkAsync(boromar, sharn, "Located in part in");
    await world.LinkAsync(boromar, breland, "Feared in");

    var page = await ListAsync(world, Caller.GameMaster, "?type=%20LOCATED%20IN%20");

    Assert.Equal(["Sharn -Located in-> Breland"], Links(page));
  }

  [Fact]
  public async Task Type_wildcards_match_literally()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    await world.LinkAsync(sharn, breland, "Located in");

    var page = await ListAsync(world, Caller.GameMaster, "?type=Located%25");

    Assert.Empty(Links(page));
  }

  [Fact]
  public async Task Pages_through_the_list()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var hub = await world.AddEntryAsync("Sharn");
    foreach (var name in new[] { "A", "B", "C" })
    {
      await world.LinkAsync(hub, await world.AddEntryAsync(name), "Near");
    }

    var page = await ListAsync(world, Caller.GameMaster, "?page=2&pageSize=2");

    Assert.Equal(["Sharn -Near-> C"], Links(page));
    Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
    Assert.Equal(2, page.GetProperty("pageSize").GetInt32());
  }

  [Fact]
  public async Task Another_settings_relationships_are_not_listed()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");
    var palanthas = await world.AddEntryAsync("Palanthas", settingId: krynnId);
    var solamnia = await world.AddEntryAsync("Solamnia", settingId: krynnId);
    await world.LinkAsync(palanthas, solamnia, "Located in");

    var page = await ListAsync(world, Caller.Owner);

    Assert.Empty(Links(page));
  }

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  public async Task Outsiders_are_refused(Caller caller, HttpStatusCode expected)
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await GetAsync(world, caller);

    Assert.Equal(expected, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
  }
}
