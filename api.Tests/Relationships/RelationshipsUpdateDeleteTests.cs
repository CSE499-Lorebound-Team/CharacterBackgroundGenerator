using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Relationships;

// P5-04: PUT and DELETE /api/settings/{sid}/relationships/{id}.
[Collection(PostgresCollection.Name)]
public class RelationshipsUpdateDeleteTests : PostgresTestBase
{
  public RelationshipsUpdateDeleteTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private sealed record Seeded(
      SharingWorld World,
      SettingEntry Sharn,
      SettingEntry Breland,
      SettingEntryRelationship Capital);

  private async Task<Seeded> SeedAsync()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    var capital = await world.LinkAsync(sharn, breland, "Capital of", "Since the Last War");
    return new Seeded(world, sharn, breland, capital);
  }

  private static string PathOf(SharingWorld world, Guid relationshipId, Guid? settingId = null) =>
      $"/api/settings/{settingId ?? world.SettingId}/relationships/{relationshipId}";

  private static Task<HttpResponseMessage> PutAsync(
      SharingWorld world, Caller caller, Guid relationshipId, object body, Guid? settingId = null) =>
      world[caller].PutAsJsonAsync(PathOf(world, relationshipId, settingId), body);

  private static Task<HttpResponseMessage> DeleteAsync(
      SharingWorld world, Caller caller, Guid relationshipId, Guid? settingId = null) =>
      world[caller].DeleteAsync(PathOf(world, relationshipId, settingId));

  private Task<SettingEntryRelationship?> FindAsync(Guid id) =>
      Factory.WithDbAsync(db => db.SettingEntryRelationships.SingleOrDefaultAsync(r => r.Id == id));

  [Theory]
  [InlineData(Caller.Owner)]
  [InlineData(Caller.GameMaster)]
  public async Task GameMasters_change_the_type_and_description(Caller caller)
  {
    var (world, sharn, breland, capital) = await SeedAsync();

    var response = await PutAsync(world, caller, capital.Id,
        new { relationshipType = "  Largest   city of ", description = "  Also the richest  " });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(response,
        "id", "source", "target", "relationshipType", "description", "createdAt", "updatedAt");
    Assert.Equal(capital.Id, body.GetProperty("id").GetGuid());
    Assert.Equal("Largest city of", body.GetProperty("relationshipType").GetString());
    Assert.Equal("Also the richest", body.GetProperty("description").GetString());
    Assert.Equal("Sharn", body.GetProperty("source").GetProperty("name").GetString());
    Assert.Equal(breland.Id, body.GetProperty("target").GetProperty("id").GetGuid());

    var stored = await FindAsync(capital.Id);
    Assert.Equal((sharn.Id, breland.Id, "Largest city of", "Also the richest"),
        (stored!.SourceEntryId, stored.TargetEntryId, stored.RelationshipType, stored.Description));
    Assert.True(stored.UpdatedAt > capital.UpdatedAt);
  }

  [Fact]
  public async Task Omitting_the_description_clears_it()
  {
    var (world, _, _, capital) = await SeedAsync();

    var response = await PutAsync(world, Caller.GameMaster, capital.Id, new { relationshipType = "Capital of" });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Null((await FindAsync(capital.Id))!.Description);
  }

  [Fact]
  public async Task Changing_the_source_or_target_is_400_and_nothing_changes()
  {
    var (world, sharn, breland, capital) = await SeedAsync();
    var aundair = await world.AddEntryAsync("Aundair");

    var newSource = await PutAsync(world, Caller.GameMaster, capital.Id,
        new { relationshipType = "Borders", sourceEntryId = aundair.Id });
    var newTarget = await PutAsync(world, Caller.GameMaster, capital.Id,
        new { relationshipType = "Borders", sourceEntryId = sharn.Id, targetEntryId = aundair.Id });

    foreach (var (response, field) in new[] { (newSource, "SourceEntryId"), (newTarget, "TargetEntryId") })
    {
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      var problem = await JsonAssert.ReadProblemAsync(response);
      Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.ToString());
    }

    var stored = await FindAsync(capital.Id);
    Assert.Equal((sharn.Id, breland.Id, "Capital of"),
        (stored!.SourceEntryId, stored.TargetEntryId, stored.RelationshipType));
  }

  [Fact]
  public async Task Sending_the_current_endpoints_is_allowed()
  {
    var (world, sharn, breland, capital) = await SeedAsync();

    var response = await PutAsync(world, Caller.GameMaster, capital.Id,
        new { relationshipType = "Borders", sourceEntryId = sharn.Id, targetEntryId = breland.Id });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Changing_to_a_type_the_link_already_has_is_409_ignoring_case()
  {
    var (world, sharn, breland, capital) = await SeedAsync();
    await world.LinkAsync(sharn, breland, "Largest city of");

    var response = await PutAsync(world, Caller.GameMaster, capital.Id, new { relationshipType = "LARGEST CITY OF" });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("Capital of", (await FindAsync(capital.Id))!.RelationshipType);
  }

  // Not a duplicate of itself. (A suggested type would be respelled by
  // Normalize, so this uses a custom one.)
  [Fact]
  public async Task Changing_only_the_case_of_its_own_type_is_fine()
  {
    var (world, _, _, capital) = await SeedAsync();
    await PutAsync(world, Caller.GameMaster, capital.Id, new { relationshipType = "Rival of" });

    var response = await PutAsync(world, Caller.GameMaster, capital.Id, new { relationshipType = "RIVAL OF" });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("RIVAL OF", (await FindAsync(capital.Id))!.RelationshipType);
  }

  [Theory]
  [InlineData("RelationshipType", "{ }")]
  [InlineData("RelationshipType", """{ "relationshipType": "  " }""")]
  [InlineData("RelationshipType", """{ "relationshipType": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }""")]
  public async Task Invalid_bodies_are_400_keyed_by_field(string field, string json)
  {
    var (world, _, _, capital) = await SeedAsync();

    var response = await world[Caller.GameMaster].PutAsync(PathOf(world, capital.Id),
        new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.ToString());
  }

  [Theory]
  [InlineData(Caller.Owner)]
  [InlineData(Caller.GameMaster)]
  public async Task GameMasters_delete_a_relationship_and_the_entries_stay(Caller caller)
  {
    var (world, _, _, capital) = await SeedAsync();

    var response = await DeleteAsync(world, caller, capital.Id);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Null(await FindAsync(capital.Id));
    Assert.Equal(2, await Factory.WithDbAsync(db => db.SettingEntries.CountAsync()));

    var again = await DeleteAsync(world, caller, capital.Id);
    Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
  }

  [Fact]
  public async Task A_relationship_of_another_setting_is_404_on_put_and_delete()
  {
    var (world, _, _, capital) = await SeedAsync();
    var krynnId = await world.CreateSettingAsync("Krynn");

    var put = await PutAsync(world, Caller.Owner, capital.Id, new { relationshipType = "Borders" }, krynnId);
    var delete = await DeleteAsync(world, Caller.Owner, capital.Id, krynnId);

    foreach (var response in new[] { put, delete })
    {
      Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
      Assert.Equal("Relationship not found.",
          (await JsonAssert.ReadProblemAsync(response)).GetProperty("detail").GetString());
    }

    Assert.Equal("Capital of", (await FindAsync(capital.Id))!.RelationshipType);
  }

  [Fact]
  public async Task A_missing_relationship_is_404()
  {
    var (world, _, _, _) = await SeedAsync();

    var put = await PutAsync(world, Caller.GameMaster, Guid.NewGuid(), new { relationshipType = "Borders" });
    var delete = await DeleteAsync(world, Caller.GameMaster, Guid.NewGuid());

    Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
  }

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(Caller.Player, HttpStatusCode.Forbidden)]
  public async Task Non_GMs_are_refused_and_nothing_changes(Caller caller, HttpStatusCode expected)
  {
    var (world, _, _, capital) = await SeedAsync();

    var put = await PutAsync(world, caller, capital.Id, new { relationshipType = "Borders" });
    var delete = await DeleteAsync(world, caller, capital.Id);

    Assert.Equal(expected, put.StatusCode);
    Assert.Equal(expected, delete.StatusCode);
    Assert.Equal("Capital of", (await FindAsync(capital.Id))!.RelationshipType);
  }
}
