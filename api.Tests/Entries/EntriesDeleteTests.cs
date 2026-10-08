using System.Net;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Entries;

// P4-06: DELETE /api/settings/{sid}/entries/{id}, 409 unless force.
[Collection(PostgresCollection.Name)]
public class EntriesDeleteTests : PostgresTestBase
{
  public EntriesDeleteTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> DeleteAsync(SharingWorld world, Caller caller, Guid entryId, string query = "") =>
      world[caller].DeleteAsync($"/api/settings/{world.SettingId}/entries/{entryId}{query}");

  private Task<(int Entries, int Relationships)> CountsAsync() =>
      Factory.WithDbAsync(async db =>
          (await db.SettingEntries.CountAsync(), await db.SettingEntryRelationships.CountAsync()));

  [Theory]
  [InlineData(Caller.Owner)]
  [InlineData(Caller.GameMaster)]
  public async Task GameMasters_delete_an_unlinked_entry(Caller caller)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var entry = await world.AddEntryAsync("Sharn");
    await world.AddEntryAsync("Breland");

    var response = await DeleteAsync(world, caller, entry.Id);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal((1, 0), await CountsAsync());
  }

  [Fact]
  public async Task A_linked_entry_is_409_with_relationshipCount_and_nothing_changes()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    var boromar = await world.AddEntryAsync("Boromar Clan", SettingEntryType.Faction);
    await world.LinkAsync(sharn, breland, "Capital of");
    await world.LinkAsync(boromar, sharn, "Based in");
    await world.LinkAsync(boromar, breland, "Operates in");

    foreach (var query in new[] { "", "?force=false" })
    {
      var response = await DeleteAsync(world, Caller.GameMaster, sharn.Id, query);

      Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
      var problem = await JsonAssert.ReadProblemAsync(response);
      Assert.Equal(2, problem.GetProperty("relationshipCount").GetInt32());
      Assert.True(problem.TryGetProperty("traceId", out _));
    }

    Assert.Equal((3, 3), await CountsAsync());
  }

  [Fact]
  public async Task Force_deletes_the_entry_and_only_its_relationships()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    var boromar = await world.AddEntryAsync("Boromar Clan", SettingEntryType.Faction);
    await world.LinkAsync(sharn, breland, "Capital of");
    await world.LinkAsync(boromar, sharn, "Based in");
    var kept = await world.LinkAsync(boromar, breland, "Operates in");

    var response = await DeleteAsync(world, Caller.GameMaster, sharn.Id, "?force=true");

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal((2, 1), await CountsAsync());
    Assert.Equal(kept.Id, await Factory.WithDbAsync(db => db.SettingEntryRelationships.Select(r => r.Id).SingleAsync()));
  }

  [Fact]
  public async Task A_player_gets_403_even_with_force()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    await world.LinkAsync(sharn, breland);

    var response = await DeleteAsync(world, Caller.Player, sharn.Id, "?force=true");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal((2, 1), await CountsAsync());
  }

  [Fact]
  public async Task A_missing_entry_or_one_of_another_setting_is_404()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");
    var palanthas = await world.AddEntryAsync("Palanthas", settingId: krynnId);

    Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(world, Caller.Owner, Guid.NewGuid())).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(world, Caller.Owner, palanthas.Id, "?force=true")).StatusCode);
    Assert.Equal((1, 0), await CountsAsync());
  }

  [Fact]
  public async Task Deleting_twice_is_404_the_second_time()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var entry = await world.AddEntryAsync("Sharn");

    Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(world, Caller.GameMaster, entry.Id)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(world, Caller.GameMaster, entry.Id)).StatusCode);
  }
}
