using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Characters;

// P6-12: the character ownership matrix, every endpoint x every kind of
// caller, through HTTP. The character belongs to the Player.
[Collection(PostgresCollection.Name)]
public class CharactersBoundaryTests : PostgresTestBase
{
  public CharactersBoundaryTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  public enum Who
  {
    OwnerMember,
    OwnerRemoved,
    GameMaster,
    SettingOwner,
    OtherPlayer,
    NonMember,
    Anonymous,
  }

  public enum Endpoint
  {
    Detail,
    Update,
    Delete,
    SettingList,
    Create,
  }

  private sealed record Scene(SharingWorld World, Character Character, HttpClient Client, Who Who);

  private async Task<Scene> SeedAsync(Who who)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player, "Aster");
    await world.AddChoiceAsync(character, "homeland", await world.AddEntryAsync("Sharn"));

    HttpClient client;
    switch (who)
    {
      case Who.OwnerRemoved:
        await world.RemoveMemberAsync(Caller.Player);
        client = world[Caller.Player];
        break;
      case Who.OtherPlayer:
        client = (await world.AddMemberAsync("OtherPlayer", SettingRole.Player)).Client;
        break;
      default:
        client = world[who switch
        {
          Who.OwnerMember => Caller.Player,
          Who.GameMaster => Caller.GameMaster,
          Who.SettingOwner => Caller.Owner,
          Who.NonMember => Caller.NonMember,
          _ => Caller.Anonymous,
        }];
        break;
    }

    return new Scene(world, character, client, who);
  }

  private static Task<HttpResponseMessage> CallAsync(Scene scene, Endpoint endpoint, Guid? characterId = null)
  {
    var id = characterId ?? scene.Character.Id;
    return endpoint switch
    {
      Endpoint.Detail => scene.Client.GetAsync($"/api/characters/{id}"),
      Endpoint.Update => scene.Client.PutAsJsonAsync($"/api/characters/{id}", new { name = "Renamed", backstory = "New." }),
      Endpoint.Delete => scene.Client.DeleteAsync($"/api/characters/{id}"),
      Endpoint.SettingList => scene.Client.GetAsync($"/api/settings/{scene.World.SettingId}/characters"),
      _ => scene.Client.PostAsJsonAsync("/api/characters", new { settingId = scene.World.SettingId, name = "New one" }),
    };
  }

  public static TheoryData<Who, Endpoint, HttpStatusCode> Matrix()
  {
    var data = new TheoryData<Who, Endpoint, HttpStatusCode>();

    void Row(Endpoint endpoint, params HttpStatusCode[] byWho)
    {
      foreach (var who in Enum.GetValues<Who>())
      {
        data.Add(who, endpoint, byWho[(int)who]);
      }
    }

    const HttpStatusCode ok = HttpStatusCode.OK;
    const HttpStatusCode created = HttpStatusCode.Created;
    const HttpStatusCode gone = HttpStatusCode.NoContent;
    const HttpStatusCode forbidden = HttpStatusCode.Forbidden;
    const HttpStatusCode notFound = HttpStatusCode.NotFound;
    const HttpStatusCode anonymous = HttpStatusCode.Unauthorized;

    //                                OwnerMember OwnerRemoved GameMaster SettingOwner OtherPlayer NonMember Anonymous
    Row(Endpoint.Detail,              ok,         ok,          ok,        ok,          notFound,   notFound, anonymous);
    Row(Endpoint.Update,              ok,         forbidden,   forbidden, forbidden,   notFound,   notFound, anonymous);
    Row(Endpoint.Delete,              gone,       gone,        forbidden, forbidden,   notFound,   notFound, anonymous);
    Row(Endpoint.SettingList,         forbidden,  notFound,    ok,        ok,          forbidden,  notFound, anonymous);
    Row(Endpoint.Create,              created,    notFound,    created,   created,     created,    notFound, anonymous);
    return data;
  }

  [Theory]
  [MemberData(nameof(Matrix))]
  public async Task Permission_matrix(Who who, Endpoint endpoint, HttpStatusCode expected)
  {
    var scene = await SeedAsync(who);
    var before = await Factory.WithDbAsync(db => db.Characters.AsNoTracking().SingleAsync(c => c.Id == scene.Character.Id));

    var response = await CallAsync(scene, endpoint);

    Assert.Equal(expected, response.StatusCode);

    // Refused writes change nothing.
    if (endpoint is Endpoint.Update or Endpoint.Delete && expected != HttpStatusCode.OK && expected != HttpStatusCode.NoContent)
    {
      var after = await Factory.WithDbAsync(db => db.Characters.AsNoTracking().SingleAsync(c => c.Id == scene.Character.Id));
      Assert.Equal((before.Name, before.Backstory, before.UpdatedAt), (after.Name, after.Backstory, after.UpdatedAt));
      Assert.Equal(1, await Factory.WithDbAsync(db => db.CharacterChoices.CountAsync()));
    }
  }

  // Someone who may not see the character cannot tell it from a missing one.
  [Theory]
  [InlineData(Who.OtherPlayer, Endpoint.Detail)]
  [InlineData(Who.OtherPlayer, Endpoint.Update)]
  [InlineData(Who.OtherPlayer, Endpoint.Delete)]
  [InlineData(Who.NonMember, Endpoint.Detail)]
  [InlineData(Who.NonMember, Endpoint.Update)]
  [InlineData(Who.NonMember, Endpoint.Delete)]
  public async Task The_404_is_identical_to_a_missing_character(Who who, Endpoint endpoint)
  {
    var scene = await SeedAsync(who);

    var real = await JsonAssert.ReadProblemAsync(await CallAsync(scene, endpoint));
    var missing = await JsonAssert.ReadProblemAsync(await CallAsync(scene, endpoint, Guid.NewGuid()));

    Assert.Equal("Character not found.", real.GetProperty("detail").GetString());
    Assert.Equal(missing.GetProperty("title").GetString(), real.GetProperty("title").GetString());
    Assert.Equal(missing.GetProperty("detail").GetString(), real.GetProperty("detail").GetString());
  }

  [Theory]
  [InlineData(Who.OwnerMember, true, false)]
  [InlineData(Who.OwnerRemoved, true, true)]
  [InlineData(Who.GameMaster, false, false)]
  [InlineData(Who.SettingOwner, false, false)]
  [InlineData(Who.OtherPlayer, false, false)]
  [InlineData(Who.NonMember, false, false)]
  public async Task The_own_list_shows_only_the_owner_and_flags_read_only(Who who, bool listed, bool readOnly)
  {
    var scene = await SeedAsync(who);

    var page = await JsonAssert.ReadJsonAsync(await scene.Client.GetAsync("/api/characters"));

    var item = page.GetProperty("items").EnumerateArray()
        .Where(i => i.GetProperty("id").GetGuid() == scene.Character.Id)
        .ToList();
    Assert.Equal(listed, item.Count == 1);
    if (listed)
    {
      Assert.Equal(readOnly, item[0].GetProperty("isReadOnly").GetBoolean());
    }
  }

  [Theory]
  [InlineData(Who.OwnerMember, true, false)]
  [InlineData(Who.OwnerRemoved, true, true)]
  [InlineData(Who.GameMaster, false, true)]
  [InlineData(Who.SettingOwner, false, true)]
  public async Task The_detail_flags_match_what_the_caller_may_do(Who who, bool isOwner, bool isReadOnly)
  {
    var scene = await SeedAsync(who);

    var body = await JsonAssert.ReadJsonAsync(await CallAsync(scene, Endpoint.Detail));

    Assert.Equal(isOwner, body.GetProperty("isOwner").GetBoolean());
    Assert.Equal(isReadOnly, body.GetProperty("isReadOnly").GetBoolean());
  }

  [Fact]
  public async Task Anonymous_is_401_on_the_own_list()
  {
    var scene = await SeedAsync(Who.Anonymous);

    Assert.Equal(HttpStatusCode.Unauthorized, (await scene.Client.GetAsync("/api/characters")).StatusCode);
  }
}
