using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Entries;

// P4-03: POST /api/settings/{sid}/entries.
[Collection(PostgresCollection.Name)]
public class EntriesCreateTests : PostgresTestBase
{
  public EntriesCreateTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> CreateAsync(SharingWorld world, Caller caller, object body) =>
      world[caller].PostAsJsonAsync($"/api/settings/{world.SettingId}/entries", body);

  private Task<int> EntryCountAsync() =>
      Factory.WithDbAsync(db => db.SettingEntries.CountAsync());

  [Theory]
  [InlineData(Caller.Owner)]
  [InlineData(Caller.GameMaster)]
  public async Task GameMasters_create_an_entry(Caller caller)
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, caller, new
    {
      name = "  Sharn  ",
      entryType = "Location",
      description = " City of Towers ",
      isGmOnly = true,
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(response,
        "id", "campaignSettingId", "name", "description", "entryType", "isGmOnly", "createdAt", "updatedAt");
    var id = body.GetProperty("id").GetGuid();
    Assert.Equal($"/api/settings/{world.SettingId}/entries/{id}", response.Headers.Location?.OriginalString);
    Assert.Equal("Sharn", body.GetProperty("name").GetString());
    Assert.Equal("City of Towers", body.GetProperty("description").GetString());
    Assert.Equal("Location", body.GetProperty("entryType").GetString());
    Assert.True(body.GetProperty("isGmOnly").GetBoolean());

    var stored = await Factory.WithDbAsync(db => db.SettingEntries.SingleAsync());
    Assert.Equal((id, world.SettingId, "Sharn", SettingEntryType.Location, true),
        (stored.Id, stored.CampaignSettingId, stored.Name, stored.EntryType, stored.IsGmOnly));
  }

  [Fact]
  public async Task Description_and_isGmOnly_are_optional()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.GameMaster, new { name = "Sharn", entryType = "Location", description = "   " });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var stored = await Factory.WithDbAsync(db => db.SettingEntries.SingleAsync());
    Assert.Null(stored.Description);
    Assert.False(stored.IsGmOnly);
  }

  [Fact]
  public async Task A_player_gets_403_and_nothing_is_created()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.Player, new { name = "Sharn", entryType = "Location" });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(0, await EntryCountAsync());
  }

  [Fact]
  public async Task A_duplicate_name_of_the_same_type_is_409_ignoring_case()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sharn", SettingEntryType.Location);

    var response = await CreateAsync(world, Caller.GameMaster, new { name = "sHARN", entryType = "Location" });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.Equal("A Location with this name already exists in this setting.", problem.GetProperty("detail").GetString());
    Assert.Equal(1, await EntryCountAsync());
  }

  [Fact]
  public async Task The_same_name_is_fine_for_another_type_or_setting()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");
    await world.AddEntryAsync("Sharn", SettingEntryType.Location);
    await world.AddEntryAsync("Sharn", SettingEntryType.Location, settingId: krynnId);

    var response = await CreateAsync(world, Caller.GameMaster, new { name = "Sharn", entryType = "Faction" });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Fact]
  public async Task Concurrent_duplicates_create_one_entry_and_the_rest_get_409()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
        CreateAsync(world, Caller.GameMaster, new { name = i % 2 == 0 ? "Sharn" : "SHARN", entryType = "Location" })));

    Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
    Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
        r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    Assert.Equal(1, await EntryCountAsync());
  }

  public static TheoryData<string, object> InvalidBodies() => new()
  {
    { "Name", new { entryType = "Location" } },
    { "Name", new { name = "   ", entryType = "Location" } },
    { "Name", new { name = new string('a', 121), entryType = "Location" } },
    { "EntryType", new { name = "Sharn" } },
    { "EntryType", new { name = "Sharn", entryType = 99 } },
    { "Description", new { name = "Sharn", entryType = "Location", description = new string('a', 4001) } },
  };

  [Theory]
  [MemberData(nameof(InvalidBodies))]
  public async Task Invalid_bodies_are_400_keyed_by_field(string field, object body)
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.GameMaster, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.ToString());
    Assert.Equal(0, await EntryCountAsync());
  }

  [Fact]
  public async Task An_unknown_type_name_is_400()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.GameMaster, new { name = "Sharn", entryType = "Planet" });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal(0, await EntryCountAsync());
  }

  [Fact]
  public async Task Name_at_the_120_character_limit_is_accepted()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.GameMaster, new { name = new string('a', 120), entryType = "Other" });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }
}
