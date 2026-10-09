using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Characters;

// P6-12: a character's life through the API, and what happens to it when
// the setting, an entry or the membership around it changes.
[Collection(PostgresCollection.Name)]
public class CharacterLifecycleTests : PostgresTestBase
{
  public CharacterLifecycleTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static async Task<Guid> CreateAsync(SharingWorld world, Caller caller, string name)
  {
    var response = await world[caller].PostAsJsonAsync("/api/characters", new { settingId = world.SettingId, name });
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return (await JsonAssert.ReadJsonAsync(response)).GetProperty("id").GetGuid();
  }

  private static Task<HttpResponseMessage> RenameAsync(SharingWorld world, Guid id, string name) =>
      world[Caller.Player].PutAsJsonAsync($"/api/characters/{id}", new { name });

  [Fact]
  public async Task Create_edit_remove_rejoin_and_delete()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var id = await CreateAsync(world, Caller.Player, "Aster");

    Assert.Equal(HttpStatusCode.OK, (await RenameAsync(world, id, "Aster Vane")).StatusCode);

    // The GM sees it in the setting.
    var gmView = await JsonAssert.ReadJsonAsync(
        await world[Caller.GameMaster].GetAsync($"/api/settings/{world.SettingId}/characters"));
    var listed = Assert.Single(gmView.GetProperty("items").EnumerateArray());
    Assert.Equal("Aster Vane", listed.GetProperty("name").GetString());

    // Removed: read-only, still listed for both.
    var removed = await world[Caller.GameMaster].DeleteAsync(
        $"/api/settings/{world.SettingId}/members/{world.UserIds[Caller.Player]}");
    Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await RenameAsync(world, id, "Blocked")).StatusCode);
    var own = await JsonAssert.ReadJsonAsync(await world[Caller.Player].GetAsync("/api/characters"));
    Assert.True(Assert.Single(own.GetProperty("items").EnumerateArray()).GetProperty("isReadOnly").GetBoolean());

    // Re-joining restores write access.
    var invite = Assert.Single(await world.AddInvitesAsync(1));
    Assert.Equal(HttpStatusCode.OK, (await world[Caller.Player].PostAsync($"/api/invites/{invite.Code}/accept", null)).StatusCode);
    var renamed = await RenameAsync(world, id, "Aster Returned");
    Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
    Assert.False((await JsonAssert.ReadJsonAsync(renamed)).GetProperty("isReadOnly").GetBoolean());

    // Deleted by the owner, gone for everyone.
    Assert.Equal(HttpStatusCode.NoContent, (await world[Caller.Player].DeleteAsync($"/api/characters/{id}")).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await world[Caller.GameMaster].GetAsync($"/api/characters/{id}")).StatusCode);
  }

  [Fact]
  public async Task Deleting_the_setting_deletes_its_characters_including_removed_players()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var playersId = await CreateAsync(world, Caller.Player, "Player's");
    var npcId = await CreateAsync(world, Caller.GameMaster, "NPC");
    await world.AddChoiceAsync(
        await Factory.WithDbAsync(db => db.Characters.SingleAsync(c => c.Id == playersId)),
        "homeland",
        await world.AddEntryAsync("Sharn"));
    await world.RemoveMemberAsync(Caller.Player);
    // A character in another setting is untouched.
    var otherSettingId = await world.CreateSettingAsync("Khorvaire");
    var elsewhere = await world.AddCharacterAsync(Caller.Owner, "Elsewhere", c => c.CampaignSettingId = otherSettingId);

    var deleted = await world[Caller.Owner].DeleteAsync($"/api/settings/{world.SettingId}");

    Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    Assert.Equal([elsewhere.Id], await Factory.WithDbAsync(db => db.Characters.Select(c => c.Id).ToListAsync()));
    Assert.False(await Factory.WithDbAsync(db => db.CharacterChoices.AnyAsync()));
    Assert.Equal(HttpStatusCode.NotFound, (await world[Caller.Player].GetAsync($"/api/characters/{playersId}")).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await world[Caller.GameMaster].GetAsync($"/api/characters/{npcId}")).StatusCode);
    Assert.Empty((await JsonAssert.ReadJsonAsync(await world[Caller.Player].GetAsync("/api/characters")))
        .GetProperty("items").EnumerateArray());
  }

  [Fact]
  public async Task Force_deleting_a_chosen_entry_leaves_the_character_with_a_null_choice()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var id = await CreateAsync(world, Caller.Player, "Aster");
    var sharn = await world.AddEntryAsync("Sharn");
    var character = await Factory.WithDbAsync(db => db.Characters.SingleAsync(c => c.Id == id));
    await world.AddChoiceAsync(character, "homeland", sharn);

    var refused = await world[Caller.GameMaster].DeleteAsync($"/api/settings/{world.SettingId}/entries/{sharn.Id}");
    Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    Assert.Equal(1, (await JsonAssert.ReadProblemAsync(refused)).GetProperty("characterCount").GetInt32());

    var forced = await world[Caller.GameMaster].DeleteAsync($"/api/settings/{world.SettingId}/entries/{sharn.Id}?force=true");
    Assert.Equal(HttpStatusCode.NoContent, forced.StatusCode);

    var detail = await JsonAssert.ReadJsonAsync(await world[Caller.Player].GetAsync($"/api/characters/{id}"));
    var choice = Assert.Single(detail.GetProperty("choices").EnumerateArray());
    Assert.Equal("homeland", choice.GetProperty("stepKey").GetString());
    Assert.Equal(JsonValueKind.Null, choice.GetProperty("entryId").ValueKind);
    Assert.Equal(JsonValueKind.Null, choice.GetProperty("entryName").ValueKind);

    var listed = await JsonAssert.ReadJsonAsync(await world[Caller.Player].GetAsync("/api/characters"));
    Assert.Equal(JsonValueKind.Null,
        Assert.Single(listed.GetProperty("items").EnumerateArray()).GetProperty("homelandName").ValueKind);
    // The character can still be edited.
    Assert.Equal(HttpStatusCode.OK, (await RenameAsync(world, id, "Still here")).StatusCode);
  }
}
