using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Characters;

// P6-06: PUT /api/characters/{id}. The character belongs to the Player.
[Collection(PostgresCollection.Name)]
public class CharactersUpdateTests : PostgresTestBase
{
  public CharactersUpdateTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> UpdateAsync(SharingWorld world, Caller caller, Guid id, object body) =>
      world[caller].PutAsJsonAsync($"/api/characters/{id}", body);

  private Task<Character> StoredAsync(Guid id) =>
      Factory.WithDbAsync(db => db.Characters.SingleAsync(c => c.Id == id));

  private async Task<(SharingWorld World, Character Character, DateTimeOffset UpdatedAt)> SeedAsync()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player, "Aster", c => c.Backstory = "Old story.");
    var hourAgo = DateTimeOffset.UtcNow.AddHours(-1);
    await Factory.WithDbAsync(db => db.Characters
        .Where(c => c.Id == character.Id)
        .ExecuteUpdateAsync(set => set.SetProperty(c => c.UpdatedAt, hourAgo)));
    return (world, character, (await StoredAsync(character.Id)).UpdatedAt);
  }

  [Fact]
  public async Task The_owner_updates_name_and_backstory()
  {
    var (world, character, before) = await SeedAsync();
    const string backstory = "  Born in Sharn.\n\n<b>Not</b> HTML, just text.  ";

    var response = await UpdateAsync(world, Caller.Player, character.Id, new { name = "  Aster Vane  ", backstory });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    Assert.Equal("Aster Vane", body.GetProperty("name").GetString());
    Assert.Equal(backstory, body.GetProperty("backstory").GetString());
    Assert.False(body.GetProperty("isReadOnly").GetBoolean());

    var stored = await StoredAsync(character.Id);
    Assert.Equal(("Aster Vane", backstory), (stored.Name, stored.Backstory));
    Assert.True(stored.UpdatedAt > before);
    Assert.Equal(stored.UpdatedAt, body.GetProperty("updatedAt").GetDateTimeOffset());
  }

  [Fact]
  public async Task Saving_unchanged_values_still_bumps_UpdatedAt()
  {
    var (world, character, before) = await SeedAsync();

    var response = await UpdateAsync(world, Caller.Player, character.Id, new { name = "Aster", backstory = "Old story." });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True((await StoredAsync(character.Id)).UpdatedAt > before);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("  \n ")]
  public async Task A_missing_or_blank_backstory_clears_it(string? backstory)
  {
    var (world, character, _) = await SeedAsync();

    var response = await UpdateAsync(world, Caller.Player, character.Id, new { name = "Aster", backstory });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Null((await StoredAsync(character.Id)).Backstory);
  }

  [Fact]
  public async Task A_backstory_of_10000_characters_is_accepted()
  {
    var (world, character, _) = await SeedAsync();

    var response = await UpdateAsync(world, Caller.Player, character.Id, new { name = "Aster", backstory = new string('b', 10000) });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  public static TheoryData<object, string> InvalidBodies => new()
  {
    { new { backstory = "No name" }, "Name" },
    { new { name = "" }, "Name" },
    { new { name = "   " }, "Name" },
    { new { name = new string('n', 101) }, "Name" },
    { new { name = "Aster", backstory = new string('b', 10001) }, "Backstory" },
  };

  [Theory]
  [MemberData(nameof(InvalidBodies))]
  public async Task Invalid_values_are_400_and_change_nothing(object body, string field)
  {
    var (world, character, before) = await SeedAsync();

    var response = await UpdateAsync(world, Caller.Player, character.Id, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    var stored = await StoredAsync(character.Id);
    Assert.Equal(("Aster", "Old story.", before), (stored.Name, stored.Backstory, stored.UpdatedAt));
  }

  [Fact]
  public async Task A_removed_owner_gets_403_with_the_reason()
  {
    var (world, character, _) = await SeedAsync();
    await world.RemoveMemberAsync(Caller.Player);

    var response = await UpdateAsync(world, Caller.Player, character.Id, new { name = "Renamed" });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(
        "This character is read-only because you are no longer a member of its setting.",
        problem.GetProperty("detail").GetString());
    Assert.Equal("Aster", (await StoredAsync(character.Id)).Name);
  }

  [Theory]
  [InlineData(Caller.GameMaster)]
  [InlineData(Caller.Owner)]
  public async Task GameMasters_of_the_setting_get_403(Caller caller)
  {
    var (world, character, _) = await SeedAsync();

    var response = await UpdateAsync(world, caller, character.Id, new { name = "Renamed" });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("Aster", (await StoredAsync(character.Id)).Name);
  }

  [Fact]
  public async Task Anyone_else_and_a_missing_character_get_404()
  {
    var (world, character, _) = await SeedAsync();
    var (otherPlayer, _) = await world.AddMemberAsync("OtherPlayer", SettingRole.Player);

    var nonMember = await UpdateAsync(world, Caller.NonMember, character.Id, new { name = "Renamed" });
    var other = await otherPlayer.PutAsJsonAsync($"/api/characters/{character.Id}", new { name = "Renamed" });
    var missing = await UpdateAsync(world, Caller.Player, Guid.NewGuid(), new { name = "Renamed" });

    Assert.Equal(HttpStatusCode.NotFound, nonMember.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    Assert.Equal("Aster", (await StoredAsync(character.Id)).Name);
  }

  [Fact]
  public async Task Anonymous_is_401()
  {
    var (world, character, _) = await SeedAsync();

    var response = await UpdateAsync(world, Caller.Anonymous, character.Id, new { name = "Renamed" });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }
}
