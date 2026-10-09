using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Characters;

// P6-03: POST /api/characters.
[Collection(PostgresCollection.Name)]
public class CharactersCreateTests : PostgresTestBase
{
  public CharactersCreateTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static readonly string[] DetailProperties =
  [
    "id", "settingId", "settingName", "ownerUserId", "ownerDisplayName", "name", "status",
    "currentStep", "backstory", "isOwner", "isReadOnly", "choices", "createdAt", "updatedAt",
  ];

  private static Task<HttpResponseMessage> CreateAsync(SharingWorld world, Caller caller, object body) =>
      world[caller].PostAsJsonAsync("/api/characters", body);

  private Task<int> CharacterCountAsync() =>
      Factory.WithDbAsync(db => db.Characters.CountAsync());

  [Theory]
  [InlineData(Caller.Player)]
  [InlineData(Caller.GameMaster)]
  [InlineData(Caller.Owner)]
  public async Task Any_member_starts_a_draft_they_own(Caller caller)
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, caller, new { settingId = world.SettingId });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(response, DetailProperties);
    var id = body.GetProperty("id").GetGuid();
    Assert.Equal($"/api/characters/{id}", response.Headers.Location?.OriginalString);
    Assert.Equal(world.SettingId, body.GetProperty("settingId").GetGuid());
    Assert.Equal("Osepia", body.GetProperty("settingName").GetString());
    Assert.Equal(world.UserIds[caller], body.GetProperty("ownerUserId").GetGuid());
    Assert.Equal(caller.ToString(), body.GetProperty("ownerDisplayName").GetString());
    Assert.Equal("Unnamed Character", body.GetProperty("name").GetString());
    Assert.Equal("Draft", body.GetProperty("status").GetString());
    Assert.Equal(1, body.GetProperty("currentStep").GetInt32());
    Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("backstory").ValueKind);
    Assert.True(body.GetProperty("isOwner").GetBoolean());
    Assert.False(body.GetProperty("isReadOnly").GetBoolean());
    Assert.Equal(0, body.GetProperty("choices").GetArrayLength());

    var stored = await Factory.WithDbAsync(db => db.Characters.SingleAsync());
    Assert.Equal((id, world.SettingId, world.UserIds[caller], CharacterStatus.Draft, 1),
        (stored.Id, stored.CampaignSettingId, stored.OwnerUserId, stored.Status, stored.CurrentStep));
  }

  [Fact]
  public async Task The_name_is_trimmed()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.Player, new { settingId = world.SettingId, name = "  Aster  " });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.Equal("Aster", (await JsonAssert.ReadJsonAsync(response)).GetProperty("name").GetString());
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task A_blank_name_becomes_the_default(string name)
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.Player, new { settingId = world.SettingId, name });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.Equal("Unnamed Character", (await JsonAssert.ReadJsonAsync(response)).GetProperty("name").GetString());
  }

  [Fact]
  public async Task A_name_over_100_characters_is_400()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var ok = await CreateAsync(world, Caller.Player, new { settingId = world.SettingId, name = new string('n', 100) });
    Assert.Equal(HttpStatusCode.Created, ok.StatusCode);

    var response = await CreateAsync(world, Caller.Player, new { settingId = world.SettingId, name = new string('n', 101) });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty("Name", out _));
    Assert.Equal(1, await CharacterCountAsync());
  }

  [Fact]
  public async Task A_missing_settingId_is_400()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.Player, new { name = "Aster" });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty("SettingId", out _));
    Assert.Equal(0, await CharacterCountAsync());
  }

  [Fact]
  public async Task A_non_member_gets_the_same_404_as_for_a_missing_setting()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var nonMember = await CreateAsync(world, Caller.NonMember, new { settingId = world.SettingId });
    var missing = await CreateAsync(world, Caller.Player, new { settingId = Guid.NewGuid() });

    Assert.Equal(HttpStatusCode.NotFound, nonMember.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    Assert.Equal(
        (await JsonAssert.ReadProblemAsync(missing)).GetProperty("detail").GetString(),
        (await JsonAssert.ReadProblemAsync(nonMember)).GetProperty("detail").GetString());
    Assert.Equal(0, await CharacterCountAsync());
  }

  [Fact]
  public async Task A_removed_player_can_no_longer_create_in_the_setting()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.RemoveMemberAsync(Caller.Player);

    var response = await CreateAsync(world, Caller.Player, new { settingId = world.SettingId });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal(0, await CharacterCountAsync());
  }

  [Fact]
  public async Task Anonymous_is_401()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.Anonymous, new { settingId = world.SettingId });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Equal(0, await CharacterCountAsync());
  }
}
