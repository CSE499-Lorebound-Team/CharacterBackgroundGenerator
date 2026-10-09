using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Characters;

// P6-10: a player removed from a setting keeps their characters, read-only,
// through the real removal, leave and invite endpoints.
[Collection(PostgresCollection.Name)]
public class CharacterReadOnlyTests : PostgresTestBase
{
  public CharacterReadOnlyTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private const string ReadOnlyDetail =
      "This character is read-only because you are no longer a member of its setting.";

  // A valid body for every character write endpoint, keyed by route
  // template, so the 403 comes from the read-only rule and not validation.
  // A new write endpoint (P7: choices, complete, reopen) must be added here.
  private static readonly Dictionary<string, object> WriteBodies = new()
  {
    ["PUT api/characters/{characterId:guid}"] = new { name = "Renamed", backstory = "Changed." },
    ["PUT api/characters/{characterId:guid}/choices/{stepKey}"] = new { freeText = "Revenge." },
  };

  private async Task<(SharingWorld World, Character Character)> SeedAsync()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var character = await world.AddCharacterAsync(Caller.Player, "Aster", c => c.Backstory = "Original.");
    await world.AddChoiceAsync(character, "homeland", sharn);
    await world.AddChoiceAsync(character, "personality", freeText: "Curious");
    return (world, character);
  }

  private Task<(string Name, string? Backstory, DateTimeOffset UpdatedAt, int Choices)> SnapshotAsync(Guid id) =>
      Factory.WithDbAsync(async db =>
      {
        var c = await db.Characters.SingleAsync(c => c.Id == id);
        var choices = await db.CharacterChoices.CountAsync(choice => choice.CharacterId == id && (choice.EntryId != null || choice.FreeText != null));
        return (c.Name, c.Backstory, c.UpdatedAt, choices);
      });

  private static async Task RemoveAsync(SharingWorld world, Caller remover)
  {
    var response = await world[remover].DeleteAsync(
        $"/api/settings/{world.SettingId}/members/{world.UserIds[Caller.Player]}");
    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  [Theory]
  [InlineData(Caller.GameMaster)]
  [InlineData(Caller.Owner)]
  [InlineData(Caller.Player)] // leaving
  public async Task Removal_or_leaving_leaves_the_characters_untouched(Caller remover)
  {
    var (world, character) = await SeedAsync();
    var before = await SnapshotAsync(character.Id);

    await RemoveAsync(world, remover);

    Assert.Equal(before, await SnapshotAsync(character.Id));
    var detail = await world[Caller.Player].GetAsync($"/api/characters/{character.Id}");
    Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(detail);
    Assert.True(body.GetProperty("isReadOnly").GetBoolean());
    Assert.Equal(2, body.GetProperty("choices").GetArrayLength());
  }

  [Fact]
  public async Task Every_character_write_endpoint_is_403_for_a_removed_owner()
  {
    var (world, character) = await SeedAsync();
    var before = await SnapshotAsync(character.Id);
    await RemoveAsync(world, Caller.GameMaster);

    var writes = CharacterWriteRoutes();
    Assert.NotEmpty(writes);

    foreach (var (method, template) in writes)
    {
      var key = $"{method} {template}";
      Assert.True(WriteBodies.ContainsKey(key), $"Add a valid sample body for {key} to {nameof(WriteBodies)}.");

      var path = "/" + template
          .Replace("{characterId:guid}", character.Id.ToString())
          .Replace("{stepKey}", CharacterStepKeys.Motivation);
      var request = new HttpRequestMessage(new HttpMethod(method), path)
      {
        Content = JsonContent.Create(WriteBodies[key]),
      };
      var response = await world[Caller.Player].SendAsync(request);

      Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{key} returned {(int)response.StatusCode}.");
      var problem = await JsonAssert.ReadProblemAsync(response);
      Assert.Equal(ReadOnlyDetail, problem.GetProperty("detail").GetString());
    }

    Assert.Equal(before, await SnapshotAsync(character.Id));
  }

  [Fact]
  public async Task A_removed_owner_can_still_read_list_and_delete()
  {
    var (world, character) = await SeedAsync();
    await RemoveAsync(world, Caller.GameMaster);

    var list = await JsonAssert.ReadJsonAsync(await world[Caller.Player].GetAsync("/api/characters"));
    var item = Assert.Single(list.GetProperty("items").EnumerateArray());
    Assert.True(item.GetProperty("isReadOnly").GetBoolean());

    var delete = await world[Caller.Player].DeleteAsync($"/api/characters/{character.Id}");
    Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    Assert.False(await Factory.WithDbAsync(db => db.Characters.AnyAsync(c => c.Id == character.Id)));
  }

  [Fact]
  public async Task Rejoining_through_a_new_invite_restores_write_access()
  {
    var (world, character) = await SeedAsync();
    await RemoveAsync(world, Caller.GameMaster);
    var invite = Assert.Single(await world.AddInvitesAsync(1));

    var accepted = await world[Caller.Player].PostAsync($"/api/invites/{invite.Code}/accept", null);
    Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

    var update = await world[Caller.Player].PutAsJsonAsync(
        $"/api/characters/{character.Id}", new { name = "Back again" });
    Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(update);
    Assert.Equal("Back again", body.GetProperty("name").GetString());
    Assert.False(body.GetProperty("isReadOnly").GetBoolean());
  }

  [Fact]
  public async Task The_setting_GameMasters_still_see_the_removed_players_character()
  {
    var (world, character) = await SeedAsync();
    await RemoveAsync(world, Caller.GameMaster);

    var detail = await world[Caller.GameMaster].GetAsync($"/api/characters/{character.Id}");
    var list = await JsonAssert.ReadJsonAsync(
        await world[Caller.GameMaster].GetAsync($"/api/settings/{world.SettingId}/characters"));

    Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    var item = Assert.Single(list.GetProperty("items").EnumerateArray());
    Assert.False(item.GetProperty("ownerIsMember").GetBoolean());
  }

  // Every route on one character that changes it, except DELETE (allowed
  // for a read-only owner by design).
  private List<(string Method, string Template)> CharacterWriteRoutes() =>
      Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
          .OfType<RouteEndpoint>()
          .Where(e => e.RoutePattern.RawText?.StartsWith("api/characters/{characterId", StringComparison.Ordinal) == true)
          .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
              .Select(method => (Method: method, Template: e.RoutePattern.RawText!)))
          .Where(r => r.Method is not ("GET" or "HEAD" or "DELETE" or "OPTIONS"))
          .ToList();
}
