using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Entries;

// P4-05: PUT /api/settings/{sid}/entries/{id}.
[Collection(PostgresCollection.Name)]
public class EntriesUpdateTests : PostgresTestBase
{
  public EntriesUpdateTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> UpdateAsync(SharingWorld world, Caller caller, Guid entryId, object body) =>
      world[caller].PutAsJsonAsync($"/api/settings/{world.SettingId}/entries/{entryId}", body);

  private Task<SettingEntry> StoredAsync(Guid entryId) =>
      Factory.WithDbAsync(db => db.SettingEntries.SingleAsync(e => e.Id == entryId));

  [Theory]
  [InlineData(Caller.Owner)]
  [InlineData(Caller.GameMaster)]
  public async Task GameMasters_update_every_field_and_UpdatedAt_moves(Caller caller)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var entry = await world.AddEntryAsync("Sharn", description: "City of Towers");
    var before = await StoredAsync(entry.Id);

    var response = await UpdateAsync(world, caller, entry.Id, new
    {
      name = " Boromar Clan ",
      entryType = "Faction",
      description = "Halfling crime family",
      isGmOnly = true,
    });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    Assert.Equal("Boromar Clan", body.GetProperty("name").GetString());
    Assert.Equal("Faction", body.GetProperty("entryType").GetString());
    Assert.True(body.GetProperty("isGmOnly").GetBoolean());

    var after = await StoredAsync(entry.Id);
    Assert.Equal(("Boromar Clan", SettingEntryType.Faction, "Halfling crime family", true),
        (after.Name, after.EntryType, after.Description, after.IsGmOnly));
    Assert.True(after.UpdatedAt > before.UpdatedAt);
    Assert.Equal(before.CreatedAt, after.CreatedAt);
  }

  [Fact]
  public async Task Secrecy_can_be_turned_off_again()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var entry = await world.AddEntryAsync("Sharn", isGmOnly: true);

    var response = await UpdateAsync(world, Caller.GameMaster, entry.Id, new { name = "Sharn", entryType = "Location" });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.False((await StoredAsync(entry.Id)).IsGmOnly);
    Assert.Equal(HttpStatusCode.OK,
        (await world[Caller.Player].GetAsync($"/api/settings/{world.SettingId}/entries/{entry.Id}")).StatusCode);
  }

  [Fact]
  public async Task Changing_only_the_case_of_its_own_name_is_allowed()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var entry = await world.AddEntryAsync("sharn");

    var response = await UpdateAsync(world, Caller.GameMaster, entry.Id, new { name = "Sharn", entryType = "Location" });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("Sharn", (await StoredAsync(entry.Id)).Name);
  }

  [Fact]
  public async Task Taking_another_entrys_name_and_type_is_409()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sharn", SettingEntryType.Location);
    var faction = await world.AddEntryAsync("Sharn", SettingEntryType.Faction);

    var response = await UpdateAsync(world, Caller.GameMaster, faction.Id, new { name = "SHARN", entryType = "Location" });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal(SettingEntryType.Faction, (await StoredAsync(faction.Id)).EntryType);
  }

  [Fact]
  public async Task A_player_gets_403_and_nothing_changes()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var entry = await world.AddEntryAsync("Sharn");

    var response = await UpdateAsync(world, Caller.Player, entry.Id, new { name = "Renamed", entryType = "Location" });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("Sharn", (await StoredAsync(entry.Id)).Name);
  }

  [Fact]
  public async Task A_missing_entry_or_one_of_another_setting_is_404()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");
    var palanthas = await world.AddEntryAsync("Palanthas", settingId: krynnId);
    var body = new { name = "Renamed", entryType = "Location" };

    Assert.Equal(HttpStatusCode.NotFound, (await UpdateAsync(world, Caller.Owner, Guid.NewGuid(), body)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await UpdateAsync(world, Caller.Owner, palanthas.Id, body)).StatusCode);
    Assert.Equal("Palanthas", (await StoredAsync(palanthas.Id)).Name);
  }

  [Fact]
  public async Task Invalid_bodies_are_400_like_create()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var entry = await world.AddEntryAsync("Sharn");

    var response = await UpdateAsync(world, Caller.GameMaster, entry.Id, new { name = new string('a', 121), entryType = 99 });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var errors = (await JsonAssert.ReadProblemAsync(response)).GetProperty("errors");
    Assert.True(errors.TryGetProperty("Name", out _));
    Assert.True(errors.TryGetProperty("EntryType", out _));
    Assert.Equal("Sharn", (await StoredAsync(entry.Id)).Name);
  }
}
