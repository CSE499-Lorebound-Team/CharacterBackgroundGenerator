using System.Net;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Entries;

// P4-04: GET /api/settings/{sid}/entries/{id}.
[Collection(PostgresCollection.Name)]
public class EntriesDetailTests : PostgresTestBase
{
  public EntriesDetailTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> GetAsync(SharingWorld world, Caller caller, Guid entryId) =>
      world[caller].GetAsync($"/api/settings/{world.SettingId}/entries/{entryId}");

  private static async Task<JsonElement> GetOkAsync(SharingWorld world, Caller caller, Guid entryId)
  {
    var response = await GetAsync(world, caller, entryId);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static List<string> OtherNames(JsonElement detail, string direction) =>
      detail.GetProperty(direction).EnumerateArray()
          .Select(r => r.GetProperty("otherEntryName").GetString()!)
          .ToList();

  [Fact]
  public async Task Returns_the_entry_with_outgoing_and_incoming_relationships()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn", description: "City of Towers");
    var breland = await world.AddEntryAsync("Breland");
    var boromar = await world.AddEntryAsync("Boromar Clan", SettingEntryType.Faction);
    var capital = await world.LinkAsync(sharn, breland, "Capital of", "Since the Last War");
    await world.LinkAsync(boromar, sharn, "Based in");

    var response = await GetAsync(world, Caller.Player, sharn.Id);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var detail = await JsonAssert.HasExactlyPropertiesAsync(response,
        "id", "campaignSettingId", "name", "entryType", "description", "outgoing", "incoming", "createdAt", "updatedAt");
    Assert.Equal("Sharn", detail.GetProperty("name").GetString());
    Assert.Equal("City of Towers", detail.GetProperty("description").GetString());

    var outgoing = Assert.Single(detail.GetProperty("outgoing").EnumerateArray());
    Assert.Equal(capital.Id, outgoing.GetProperty("id").GetGuid());
    Assert.Equal(breland.Id, outgoing.GetProperty("otherEntryId").GetGuid());
    Assert.Equal("Breland", outgoing.GetProperty("otherEntryName").GetString());
    Assert.Equal("Location", outgoing.GetProperty("otherEntryType").GetString());
    Assert.Equal("Capital of", outgoing.GetProperty("relationshipType").GetString());
    Assert.Equal("Since the Last War", outgoing.GetProperty("description").GetString());

    var incoming = Assert.Single(detail.GetProperty("incoming").EnumerateArray());
    Assert.Equal(boromar.Id, incoming.GetProperty("otherEntryId").GetGuid());
    Assert.Equal("Faction", incoming.GetProperty("otherEntryType").GetString());
    Assert.Equal("Based in", incoming.GetProperty("relationshipType").GetString());
  }

  [Fact]
  public async Task GameMasters_get_the_isGmOnly_flag()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var lord = await world.AddEntryAsync("The Lord of Blades", SettingEntryType.Person, isGmOnly: true);

    var detail = await GetOkAsync(world, Caller.GameMaster, lord.Id);

    Assert.True(detail.GetProperty("isGmOnly").GetBoolean());
  }

  [Fact]
  public async Task A_player_gets_the_same_404_for_a_hidden_entry_as_for_a_missing_one()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var lord = await world.AddEntryAsync("The Lord of Blades", SettingEntryType.Person, isGmOnly: true);

    var hidden = await GetAsync(world, Caller.Player, lord.Id);
    var missing = await GetAsync(world, Caller.Player, Guid.NewGuid());

    Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    Assert.Equal(
        (await JsonAssert.ReadProblemAsync(missing)).GetProperty("detail").GetString(),
        (await JsonAssert.ReadProblemAsync(hidden)).GetProperty("detail").GetString());
  }

  [Fact]
  public async Task Relationships_to_hidden_entries_are_left_out_for_players_only()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    var lord = await world.AddEntryAsync("The Lord of Blades", SettingEntryType.Person, isGmOnly: true);
    var vault = await world.AddEntryAsync("Hidden Vault", isGmOnly: true);
    await world.LinkAsync(sharn, breland, "Capital of");
    await world.LinkAsync(sharn, vault, "Hides");
    await world.LinkAsync(lord, sharn, "Spies on");

    var forPlayer = await GetOkAsync(world, Caller.Player, sharn.Id);
    var forGm = await GetOkAsync(world, Caller.GameMaster, sharn.Id);

    Assert.Equal(["Breland"], OtherNames(forPlayer, "outgoing"));
    Assert.Empty(OtherNames(forPlayer, "incoming"));
    Assert.Equal(["Breland", "Hidden Vault"], OtherNames(forGm, "outgoing"));
    Assert.Equal(["The Lord of Blades"], OtherNames(forGm, "incoming"));
  }

  [Fact]
  public async Task An_entry_of_another_setting_is_404_through_this_one()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");
    var palanthas = await world.AddEntryAsync("Palanthas", settingId: krynnId);

    var response = await GetAsync(world, Caller.Owner, palanthas.Id);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }
}
