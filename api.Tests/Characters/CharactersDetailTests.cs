using System.Net;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Characters;

// P6-05: GET /api/characters/{id}. The character belongs to the Player.
[Collection(PostgresCollection.Name)]
public class CharactersDetailTests : PostgresTestBase
{
  public CharactersDetailTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> GetAsync(SharingWorld world, Caller caller, Guid id) =>
      world[caller].GetAsync($"/api/characters/{id}");

  private static async Task<JsonElement> ReadOkAsync(SharingWorld world, Caller caller, Guid id)
  {
    var response = await GetAsync(world, caller, id);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  [Fact]
  public async Task The_owner_reads_the_character_with_resolved_choices_in_order()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var guild = await world.AddEntryAsync("House Cannith", SettingEntryType.Faction);
    var character = await world.AddCharacterAsync(Caller.Player, "Aster", c =>
    {
      c.Backstory = "Raised in the towers.";
      c.CurrentStep = 3;
    });
    await world.AddChoiceAsync(character, "skills", freeText: "Forgery", ordinal: 1);
    await world.AddChoiceAsync(character, "skills", freeText: "Riding", ordinal: 0);
    await world.AddChoiceAsync(character, "homeland", sharn);
    await world.AddChoiceAsync(character, "faction", guild);

    var body = await ReadOkAsync(world, Caller.Player, character.Id);

    Assert.Equal(character.Id, body.GetProperty("id").GetGuid());
    Assert.Equal("Aster", body.GetProperty("name").GetString());
    Assert.Equal("Raised in the towers.", body.GetProperty("backstory").GetString());
    Assert.Equal(3, body.GetProperty("currentStep").GetInt32());
    Assert.Equal("Player", body.GetProperty("ownerDisplayName").GetString());
    Assert.True(body.GetProperty("isOwner").GetBoolean());
    Assert.False(body.GetProperty("isReadOnly").GetBoolean());

    var choices = body.GetProperty("choices").EnumerateArray().ToList();
    Assert.Equal(
        ["faction/0", "homeland/0", "skills/0", "skills/1"],
        choices.Select(c => $"{c.GetProperty("stepKey").GetString()}/{c.GetProperty("ordinal").GetInt32()}"));

    var homeland = choices[1];
    Assert.Equal(
        ["stepKey", "ordinal", "entryId", "entryName", "entryType", "freeText"],
        homeland.EnumerateObject().Select(p => p.Name));
    Assert.Equal(sharn.Id, homeland.GetProperty("entryId").GetGuid());
    Assert.Equal("Sharn", homeland.GetProperty("entryName").GetString());
    Assert.Equal("Location", homeland.GetProperty("entryType").GetString());
    Assert.Equal(JsonValueKind.Null, homeland.GetProperty("freeText").ValueKind);

    var riding = choices[2];
    Assert.Equal("Riding", riding.GetProperty("freeText").GetString());
    Assert.Equal(JsonValueKind.Null, riding.GetProperty("entryId").ValueKind);
    Assert.Equal(JsonValueKind.Null, riding.GetProperty("entryName").ValueKind);
    Assert.Equal(JsonValueKind.Null, riding.GetProperty("entryType").ValueKind);
  }

  [Theory]
  [InlineData(Caller.GameMaster)]
  [InlineData(Caller.Owner)]
  public async Task GameMasters_of_the_setting_read_it_read_only(Caller caller)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player);

    var body = await ReadOkAsync(world, caller, character.Id);

    Assert.Equal(world.UserIds[Caller.Player], body.GetProperty("ownerUserId").GetGuid());
    Assert.False(body.GetProperty("isOwner").GetBoolean());
    Assert.True(body.GetProperty("isReadOnly").GetBoolean());
  }

  [Fact]
  public async Task A_removed_owner_still_reads_it_read_only()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.RemoveMemberAsync(Caller.Player);

    var body = await ReadOkAsync(world, Caller.Player, character.Id);

    Assert.True(body.GetProperty("isOwner").GetBoolean());
    Assert.True(body.GetProperty("isReadOnly").GetBoolean());
  }

  [Theory]
  [InlineData(Caller.NonMember)]
  [InlineData(Caller.Player)]
  public async Task Anyone_else_gets_the_same_404_as_for_a_missing_character(Caller caller)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    // Caller.Player is "another player" here: the character is the GM's.
    var character = await world.AddCharacterAsync(Caller.GameMaster);

    var response = await GetAsync(world, caller, character.Id);
    var missing = await GetAsync(world, caller, Guid.NewGuid());

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.Equal("Character not found.", problem.GetProperty("detail").GetString());
    Assert.Equal(
        (await JsonAssert.ReadProblemAsync(missing)).GetProperty("detail").GetString(),
        problem.GetProperty("detail").GetString());
  }

  [Fact]
  public async Task Anonymous_is_401()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player);

    var response = await GetAsync(world, Caller.Anonymous, character.Id);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // Decision in P6-05: hiding an entry afterwards does not break or hide
  // the choice for the owner.
  [Fact]
  public async Task A_chosen_entry_that_became_GM_only_keeps_its_name_for_the_owner()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.AddChoiceAsync(character, "homeland", sharn);
    await Factory.WithDbAsync(db => db.SettingEntries
        .Where(e => e.Id == sharn.Id)
        .ExecuteUpdateAsync(set => set.SetProperty(e => e.IsGmOnly, true)));

    var body = await ReadOkAsync(world, Caller.Player, character.Id);

    var choice = Assert.Single(body.GetProperty("choices").EnumerateArray());
    Assert.Equal(sharn.Id, choice.GetProperty("entryId").GetGuid());
    Assert.Equal("Sharn", choice.GetProperty("entryName").GetString());
  }

  [Fact]
  public async Task A_deleted_entry_appears_with_a_null_name()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.AddChoiceAsync(character, "homeland", sharn);
    await Factory.WithDbAsync(db => db.SettingEntries.Where(e => e.Id == sharn.Id).ExecuteDeleteAsync());

    var body = await ReadOkAsync(world, Caller.Player, character.Id);

    var choice = Assert.Single(body.GetProperty("choices").EnumerateArray());
    Assert.Equal("homeland", choice.GetProperty("stepKey").GetString());
    Assert.Equal(JsonValueKind.Null, choice.GetProperty("entryId").ValueKind);
    Assert.Equal(JsonValueKind.Null, choice.GetProperty("entryName").ValueKind);
    Assert.Equal(JsonValueKind.Null, choice.GetProperty("entryType").ValueKind);
  }
}
