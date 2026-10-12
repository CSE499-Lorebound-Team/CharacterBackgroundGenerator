using System.Net;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Characters;

// P6-07: DELETE /api/characters/{id}. The character belongs to the Player.
[Collection(PostgresCollection.Name)]
public class CharactersDeleteTests : PostgresTestBase
{
  public CharactersDeleteTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> DeleteAsync(SharingWorld world, Caller caller, Guid id) =>
      world[caller].DeleteAsync($"/api/characters/{id}");

  private Task<bool> ExistsAsync(Guid id) =>
      Factory.WithDbAsync(db => db.Characters.AnyAsync(c => c.Id == id));

  private async Task<(SharingWorld World, Character Character, SettingEntry Entry)> SeedAsync()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var entry = await world.AddEntryAsync("Sharn");
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.AddChoiceAsync(character, "homeland", entry);
    await world.AddChoiceAsync(character, "personality", freeText: "Curious");
    return (world, character, entry);
  }

  [Fact]
  public async Task The_owner_deletes_the_character_and_its_choices()
  {
    var (world, character, entry) = await SeedAsync();
    var other = await world.AddCharacterAsync(Caller.Player, "Kept");
    await world.AddChoiceAsync(other, "homeland", entry);

    var response = await DeleteAsync(world, Caller.Player, character.Id);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.False(await ExistsAsync(character.Id));
    Assert.False(await Factory.WithDbAsync(db => db.CharacterChoices.AnyAsync(c => c.CharacterId == character.Id)));
    Assert.True(await ExistsAsync(other.Id));
    Assert.Equal(1, await Factory.WithDbAsync(db => db.CharacterChoices.CountAsync()));
    Assert.True(await Factory.WithDbAsync(db => db.SettingEntries.AnyAsync(e => e.Id == entry.Id)));

    var again = await DeleteAsync(world, Caller.Player, character.Id);
    Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
  }

  [Fact]
  public async Task A_removed_owner_can_still_delete_their_read_only_character()
  {
    var (world, character, _) = await SeedAsync();
    await world.RemoveMemberAsync(Caller.Player);

    var response = await DeleteAsync(world, Caller.Player, character.Id);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.False(await ExistsAsync(character.Id));
  }

  [Theory]
  [InlineData(Caller.GameMaster)]
  [InlineData(Caller.Owner)]
  public async Task A_GameMaster_cannot_delete_another_players_character(Caller caller)
  {
    var (world, character, _) = await SeedAsync();

    var response = await DeleteAsync(world, caller, character.Id);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.Equal("Only the character's owner can do this.", problem.GetProperty("detail").GetString());
    Assert.True(await ExistsAsync(character.Id));
  }

  [Fact]
  public async Task A_GameMaster_deletes_their_own_character()
  {
    var (world, _, _) = await SeedAsync();
    var npc = await world.AddCharacterAsync(Caller.GameMaster, "NPC");

    var response = await DeleteAsync(world, Caller.GameMaster, npc.Id);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.False(await ExistsAsync(npc.Id));
  }

  [Fact]
  public async Task Anyone_else_and_a_missing_character_get_404()
  {
    var (world, character, _) = await SeedAsync();
    var (otherPlayer, _) = await world.AddMemberAsync("OtherPlayer", SettingRole.Player);

    var nonMember = await DeleteAsync(world, Caller.NonMember, character.Id);
    var other = await otherPlayer.DeleteAsync($"/api/characters/{character.Id}");
    var missing = await DeleteAsync(world, Caller.Player, Guid.NewGuid());

    Assert.Equal(HttpStatusCode.NotFound, nonMember.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    Assert.True(await ExistsAsync(character.Id));
  }

  [Fact]
  public async Task Anonymous_is_401()
  {
    var (world, character, _) = await SeedAsync();

    var response = await DeleteAsync(world, Caller.Anonymous, character.Id);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.True(await ExistsAsync(character.Id));
  }
}
