using System.Net;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Characters;

// P6-08: GET /api/settings/{sid}/characters, the GameMaster view.
[Collection(PostgresCollection.Name)]
public class SettingCharactersListTests : PostgresTestBase
{
  public SettingCharactersListTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> ListAsync(SharingWorld world, Caller caller, string query = "", Guid? settingId = null) =>
      world[caller].GetAsync($"/api/settings/{settingId ?? world.SettingId}/characters{query}");

  private static async Task<JsonElement> ReadOkAsync(SharingWorld world, Caller caller, string query = "")
  {
    var response = await ListAsync(world, caller, query);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static List<string> Names(JsonElement page) =>
      page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToList();

  private Task SetUpdatedAtAsync(Character character, int minutesAgo) =>
      Factory.WithDbAsync(db => db.Characters
          .Where(c => c.Id == character.Id)
          .ExecuteUpdateAsync(set => set.SetProperty(
              c => c.UpdatedAt, DateTimeOffset.UtcNow.AddMinutes(-minutesAgo))));

  [Theory]
  [InlineData(Caller.GameMaster)]
  [InlineData(Caller.Owner)]
  public async Task GameMasters_see_every_character_in_the_setting_newest_first(Caller caller)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var players = await world.AddCharacterAsync(Caller.Player, "Player's");
    var npc = await world.AddCharacterAsync(Caller.GameMaster, "NPC");
    var otherSettingId = await world.CreateSettingAsync("Khorvaire");
    await world.AddCharacterAsync(Caller.Owner, "Elsewhere", c => c.CampaignSettingId = otherSettingId);
    await SetUpdatedAtAsync(players, 5);
    await SetUpdatedAtAsync(npc, 1);

    var page = await ReadOkAsync(world, caller);

    Assert.Equal(["NPC", "Player's"], Names(page));
    Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
  }

  [Fact]
  public async Task An_item_includes_the_owner_and_has_exactly_the_documented_fields()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player, "Aster", c => c.CurrentStep = 2);
    await world.AddChoiceAsync(character, "homeland", await world.AddEntryAsync("Sharn"));

    var item = Assert.Single((await ReadOkAsync(world, Caller.GameMaster)).GetProperty("items").EnumerateArray());

    Assert.Equal(
        new[] { "currentStep", "homelandName", "id", "name", "ownerDisplayName", "ownerIsMember", "ownerUserId", "status", "updatedAt" },
        item.EnumerateObject().Select(p => p.Name).Order());
    Assert.Equal(character.Id, item.GetProperty("id").GetGuid());
    Assert.Equal(world.UserIds[Caller.Player], item.GetProperty("ownerUserId").GetGuid());
    Assert.Equal("Player", item.GetProperty("ownerDisplayName").GetString());
    Assert.True(item.GetProperty("ownerIsMember").GetBoolean());
    Assert.Equal("Sharn", item.GetProperty("homelandName").GetString());
    Assert.Equal("Draft", item.GetProperty("status").GetString());
    Assert.Equal(2, item.GetProperty("currentStep").GetInt32());
  }

  [Fact]
  public async Task Characters_of_removed_players_stay_listed()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddCharacterAsync(Caller.Player, "Left behind");
    await world.AddCharacterAsync(Caller.Owner, "Owner's");
    await world.RemoveMemberAsync(Caller.Player);
    // The setting owner always counts as a member, even without a row.
    await world.RemoveMemberAsync(Caller.Owner);

    var members = (await ReadOkAsync(world, Caller.GameMaster)).GetProperty("items").EnumerateArray()
        .ToDictionary(i => i.GetProperty("name").GetString()!, i => i.GetProperty("ownerIsMember").GetBoolean());

    Assert.False(members["Left behind"]);
    Assert.True(members["Owner's"]);
  }

  [Fact]
  public async Task Filters_by_status_and_by_character_or_owner_name()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddCharacterAsync(Caller.Player, "Aster");
    await world.AddCharacterAsync(Caller.Player, "Brim", c => c.Status = CharacterStatus.Complete);
    await world.AddCharacterAsync(Caller.GameMaster, "Cassia_%");

    Assert.Equal(["Brim"], Names(await ReadOkAsync(world, Caller.GameMaster, "?status=Complete")));
    Assert.Equal(["Brim"], Names(await ReadOkAsync(world, Caller.GameMaster, "?search=%20BRIM%20")));
    Assert.Equal(["Aster", "Brim"], Names(await ReadOkAsync(world, Caller.GameMaster, "?search=player")).Order());
    Assert.Equal(["Cassia_%"], Names(await ReadOkAsync(world, Caller.GameMaster, "?search=_%25")));
    Assert.Equal(["Aster"], Names(await ReadOkAsync(world, Caller.GameMaster, "?status=Draft&search=player")));
  }

  [Fact]
  public async Task Pages_through_the_list()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    for (var i = 1; i <= 5; i++)
    {
      await SetUpdatedAtAsync(await world.AddCharacterAsync(Caller.Player, $"Character {i}"), i);
    }

    var page = await ReadOkAsync(world, Caller.GameMaster, "?page=3&pageSize=2");

    Assert.Equal(["Character 5"], Names(page));
    Assert.Equal(5, page.GetProperty("totalCount").GetInt32());
  }

  [Fact]
  public async Task A_Player_gets_403()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddCharacterAsync(Caller.Player);

    var response = await ListAsync(world, Caller.Player);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task A_non_member_gets_the_same_404_as_for_a_missing_setting()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var nonMember = await ListAsync(world, Caller.NonMember);
    var missing = await ListAsync(world, Caller.GameMaster, settingId: Guid.NewGuid());

    Assert.Equal(HttpStatusCode.NotFound, nonMember.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    Assert.Equal(
        (await JsonAssert.ReadProblemAsync(missing)).GetProperty("detail").GetString(),
        (await JsonAssert.ReadProblemAsync(nonMember)).GetProperty("detail").GetString());
  }

  [Fact]
  public async Task Anonymous_is_401()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await ListAsync(world, Caller.Anonymous);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }
}
