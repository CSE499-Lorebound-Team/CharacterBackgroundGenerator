using System.Net;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Characters;

// P6-04: GET /api/characters, the caller's own characters.
[Collection(PostgresCollection.Name)]
public class CharactersListTests : PostgresTestBase
{
  public CharactersListTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static async Task<JsonElement> ListAsync(SharingWorld world, Caller caller, string query = "")
  {
    var response = await world[caller].GetAsync($"/api/characters{query}");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static List<string> Names(JsonElement page) =>
      page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToList();

  // SaveChanges stamps UpdatedAt with the current time; tests that depend on
  // the order set it explicitly.
  private Task SetUpdatedAtAsync(Character character, int minutesAgo) =>
      Factory.WithDbAsync(db => db.Characters
          .Where(c => c.Id == character.Id)
          .ExecuteUpdateAsync(set => set.SetProperty(
              c => c.UpdatedAt, DateTimeOffset.UtcNow.AddMinutes(-minutesAgo))));

  [Fact]
  public async Task Lists_only_the_callers_characters_most_recently_updated_first()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var older = await world.AddCharacterAsync(Caller.Player, "Older");
    var newer = await world.AddCharacterAsync(Caller.Player, "Newer");
    await world.AddCharacterAsync(Caller.GameMaster, "The GM's");
    await SetUpdatedAtAsync(older, 10);
    await SetUpdatedAtAsync(newer, 1);

    var page = await ListAsync(world, Caller.Player);

    Assert.Equal(["Newer", "Older"], Names(page));
    Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
    Assert.Equal(["The GM's"], Names(await ListAsync(world, Caller.GameMaster)));
    Assert.Empty(Names(await ListAsync(world, Caller.NonMember)));
  }

  [Fact]
  public async Task An_item_has_exactly_the_documented_fields()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player, "Aster", c =>
    {
      c.CurrentStep = 4;
      c.Status = CharacterStatus.Complete;
    });

    var page = await ListAsync(world, Caller.Player);

    var item = Assert.Single(page.GetProperty("items").EnumerateArray());
    Assert.Equal(
        new[] { "currentStep", "homelandName", "id", "isReadOnly", "name", "settingId", "settingName", "status", "updatedAt" },
        item.EnumerateObject().Select(p => p.Name).Order());
    Assert.Equal(character.Id, item.GetProperty("id").GetGuid());
    Assert.Equal("Complete", item.GetProperty("status").GetString());
    Assert.Equal(world.SettingId, item.GetProperty("settingId").GetGuid());
    Assert.Equal("Osepia", item.GetProperty("settingName").GetString());
    Assert.Equal(4, item.GetProperty("currentStep").GetInt32());
    Assert.False(item.GetProperty("isReadOnly").GetBoolean());
    Assert.Equal(JsonValueKind.Null, item.GetProperty("homelandName").ValueKind);
  }

  [Fact]
  public async Task HomelandName_comes_from_the_homeland_choice()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var fromEntry = await world.AddCharacterAsync(Caller.Player, "From entry");
    await world.AddChoiceAsync(fromEntry, "homeland", sharn);
    await world.AddChoiceAsync(fromEntry, "faction", await world.AddEntryAsync("Cannith", SettingEntryType.Faction));
    var fromText = await world.AddCharacterAsync(Caller.Player, "From text");
    await world.AddChoiceAsync(fromText, "homeland", freeText: "A village with no name");
    var deleted = await world.AddCharacterAsync(Caller.Player, "Deleted entry");
    var lost = await world.AddEntryAsync("Cyre");
    await world.AddChoiceAsync(deleted, "homeland", lost);
    await Factory.WithDbAsync(db => db.SettingEntries.Where(e => e.Id == lost.Id).ExecuteDeleteAsync());

    var page = await ListAsync(world, Caller.Player);

    var homelands = page.GetProperty("items").EnumerateArray().ToDictionary(
        i => i.GetProperty("name").GetString()!,
        i => i.GetProperty("homelandName").GetString());
    Assert.Equal("Sharn", homelands["From entry"]);
    Assert.Equal("A village with no name", homelands["From text"]);
    Assert.Null(homelands["Deleted entry"]);
  }

  [Fact]
  public async Task IsReadOnly_is_true_after_removal_from_the_setting()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddCharacterAsync(Caller.Player);
    await world.AddCharacterAsync(Caller.Owner, "Owner's");

    await world.RemoveMemberAsync(Caller.Player);

    var item = Assert.Single(
        (await ListAsync(world, Caller.Player)).GetProperty("items").EnumerateArray());
    Assert.True(item.GetProperty("isReadOnly").GetBoolean());
    // The setting owner is always a member, even without a membership row.
    await world.RemoveMemberAsync(Caller.Owner);
    var ownerItem = Assert.Single(
        (await ListAsync(world, Caller.Owner)).GetProperty("items").EnumerateArray());
    Assert.False(ownerItem.GetProperty("isReadOnly").GetBoolean());
  }

  [Fact]
  public async Task Filters_by_status_setting_and_name()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var otherSettingId = await world.CreateSettingAsync("Khorvaire");
    await world.AddCharacterAsync(Caller.Owner, "Draft in Osepia");
    await world.AddCharacterAsync(Caller.Owner, "Done in Osepia", c => c.Status = CharacterStatus.Complete);
    await world.AddCharacterAsync(Caller.Owner, "Draft in Khorvaire", c => c.CampaignSettingId = otherSettingId);
    await world.AddCharacterAsync(Caller.Owner, "100%_sure");

    Assert.Equal(["Done in Osepia"], Names(await ListAsync(world, Caller.Owner, "?status=Complete")));
    Assert.Equal(["Draft in Khorvaire"], Names(await ListAsync(world, Caller.Owner, $"?settingId={otherSettingId}")));
    Assert.Equal(
        ["Draft in Khorvaire", "Draft in Osepia"],
        Names(await ListAsync(world, Caller.Owner, "?search=%20DRAFT%20")).Order());
    // Wildcards match literally.
    Assert.Equal(["100%_sure"], Names(await ListAsync(world, Caller.Owner, "?search=%25_")));
    Assert.Equal(
        ["Draft in Osepia"],
        Names(await ListAsync(world, Caller.Owner, $"?status=Draft&settingId={world.SettingId}&search=draft")));
  }

  [Fact]
  public async Task Pages_through_the_list()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    for (var i = 1; i <= 5; i++)
    {
      var character = await world.AddCharacterAsync(Caller.Player, $"Character {i}");
      await SetUpdatedAtAsync(character, i);
    }

    var page = await ListAsync(world, Caller.Player, "?page=2&pageSize=2");

    Assert.Equal(["Character 3", "Character 4"], Names(page));
    Assert.Equal(2, page.GetProperty("page").GetInt32());
    Assert.Equal(2, page.GetProperty("pageSize").GetInt32());
    Assert.Equal(5, page.GetProperty("totalCount").GetInt32());
  }

  [Fact]
  public async Task An_unknown_status_is_400()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[Caller.Player].GetAsync("/api/characters?status=Retired");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Anonymous_is_401()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[Caller.Anonymous].GetAsync("/api/characters");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }
}
