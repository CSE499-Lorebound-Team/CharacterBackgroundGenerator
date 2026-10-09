using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Relationships;

// P5-03: POST /api/settings/{sid}/relationships.
[Collection(PostgresCollection.Name)]
public class RelationshipsCreateTests : PostgresTestBase
{
  public RelationshipsCreateTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> CreateAsync(
      SharingWorld world, Caller caller, object body, Guid? settingId = null) =>
      world[caller].PostAsJsonAsync($"/api/settings/{settingId ?? world.SettingId}/relationships", body);

  private Task<int> RelationshipCountAsync() =>
      Factory.WithDbAsync(db => db.SettingEntryRelationships.CountAsync());

  private static async Task<(SharingWorld World, SettingEntry Sharn, SettingEntry Breland)> SeedAsync(
      CustomWebApplicationFactory factory)
  {
    var world = await SharingWorld.SeedAsync(factory);
    return (world, await world.AddEntryAsync("Sharn"), await world.AddEntryAsync("Breland"));
  }

  private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field)
  {
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.ToString());
  }

  [Theory]
  [InlineData(Caller.Owner)]
  [InlineData(Caller.GameMaster)]
  public async Task GameMasters_link_two_entries(Caller caller)
  {
    var (world, sharn, breland) = await SeedAsync(Factory);

    var response = await CreateAsync(world, caller, new
    {
      sourceEntryId = sharn.Id,
      targetEntryId = breland.Id,
      relationshipType = "  Capital of  ",
      description = " Since the Last War ",
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(response,
        "id", "source", "target", "relationshipType", "description", "createdAt", "updatedAt");
    var id = body.GetProperty("id").GetGuid();
    Assert.Equal($"/api/settings/{world.SettingId}/relationships/{id}", response.Headers.Location?.OriginalString);
    Assert.Equal("Capital of", body.GetProperty("relationshipType").GetString());
    Assert.Equal("Since the Last War", body.GetProperty("description").GetString());
    Assert.Equal(sharn.Id, body.GetProperty("source").GetProperty("id").GetGuid());
    Assert.Equal("Sharn", body.GetProperty("source").GetProperty("name").GetString());
    Assert.Equal("Breland", body.GetProperty("target").GetProperty("name").GetString());
    Assert.Equal("Location", body.GetProperty("target").GetProperty("entryType").GetString());

    var stored = await Factory.WithDbAsync(db => db.SettingEntryRelationships.SingleAsync());
    Assert.Equal((id, world.SettingId, sharn.Id, breland.Id, "Capital of"),
        (stored.Id, stored.CampaignSettingId, stored.SourceEntryId, stored.TargetEntryId, stored.RelationshipType));
  }

  [Fact]
  public async Task Description_is_optional_and_blank_becomes_null()
  {
    var (world, sharn, breland) = await SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.GameMaster, new
    {
      sourceEntryId = sharn.Id,
      targetEntryId = breland.Id,
      relationshipType = "Capital of",
      description = "   ",
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.Null((await Factory.WithDbAsync(db => db.SettingEntryRelationships.SingleAsync())).Description);
  }

  [Fact]
  public async Task GM_only_entries_can_be_linked()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var lord = await world.AddEntryAsync("The Lord of Blades", SettingEntryType.Person, isGmOnly: true);

    var response = await CreateAsync(world, Caller.GameMaster,
        new { sourceEntryId = lord.Id, targetEntryId = sharn.Id, relationshipType = "Spies on" });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Fact]
  public async Task The_setting_comes_from_the_route_not_the_body()
  {
    var (world, sharn, breland) = await SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");

    var response = await CreateAsync(world, Caller.Owner, new
    {
      campaignSettingId = krynnId,
      sourceEntryId = sharn.Id,
      targetEntryId = breland.Id,
      relationshipType = "Capital of",
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var stored = await Factory.WithDbAsync(db => db.SettingEntryRelationships.SingleAsync());
    Assert.Equal(world.SettingId, stored.CampaignSettingId);
  }

  [Fact]
  public async Task An_entry_of_another_setting_is_400_and_nothing_is_created()
  {
    var (world, sharn, _) = await SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");
    var palanthas = await world.AddEntryAsync("Palanthas", settingId: krynnId);

    // Into this setting with a Krynn target, and into Krynn with an Osepia source.
    var intoOsepia = await CreateAsync(world, Caller.Owner,
        new { sourceEntryId = sharn.Id, targetEntryId = palanthas.Id, relationshipType = "Trades with" });
    var intoKrynn = await CreateAsync(world, Caller.Owner,
        new { sourceEntryId = sharn.Id, targetEntryId = palanthas.Id, relationshipType = "Trades with" },
        settingId: krynnId);

    await AssertFieldErrorAsync(intoOsepia, "TargetEntryId");
    await AssertFieldErrorAsync(intoKrynn, "SourceEntryId");
    Assert.Equal(0, await RelationshipCountAsync());
  }

  [Fact]
  public async Task A_missing_entry_is_400_keyed_by_field()
  {
    var (world, sharn, _) = await SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.GameMaster,
        new { sourceEntryId = Guid.NewGuid(), targetEntryId = sharn.Id, relationshipType = "Near" });

    await AssertFieldErrorAsync(response, "SourceEntryId");
    Assert.Equal(0, await RelationshipCountAsync());
  }

  [Fact]
  public async Task A_self_link_is_400()
  {
    var (world, sharn, _) = await SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.GameMaster,
        new { sourceEntryId = sharn.Id, targetEntryId = sharn.Id, relationshipType = "Near" });

    await AssertFieldErrorAsync(response, "TargetEntryId");
    Assert.Equal(0, await RelationshipCountAsync());
  }

  [Fact]
  public async Task A_duplicate_link_is_409_but_another_type_or_direction_is_fine()
  {
    var (world, sharn, breland) = await SeedAsync(Factory);
    await world.LinkAsync(sharn, breland, "Capital of");

    var duplicate = await CreateAsync(world, Caller.GameMaster,
        new { sourceEntryId = sharn.Id, targetEntryId = breland.Id, relationshipType = " Capital of " });
    var otherType = await CreateAsync(world, Caller.GameMaster,
        new { sourceEntryId = sharn.Id, targetEntryId = breland.Id, relationshipType = "Largest city of" });
    var reversed = await CreateAsync(world, Caller.GameMaster,
        new { sourceEntryId = breland.Id, targetEntryId = sharn.Id, relationshipType = "Capital of" });

    Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    Assert.Equal("These entries are already linked with this relationship type.",
        (await JsonAssert.ReadProblemAsync(duplicate)).GetProperty("detail").GetString());
    Assert.Equal(HttpStatusCode.Created, otherType.StatusCode);
    Assert.Equal(HttpStatusCode.Created, reversed.StatusCode);
    Assert.Equal(3, await RelationshipCountAsync());
  }

  [Fact]
  public async Task Concurrent_duplicates_create_one_link_and_the_rest_get_409()
  {
    var (world, sharn, breland) = await SeedAsync(Factory);

    var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
        CreateAsync(world, Caller.GameMaster,
            new { sourceEntryId = sharn.Id, targetEntryId = breland.Id, relationshipType = "Capital of" })));

    Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
    Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
        r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    Assert.Equal(1, await RelationshipCountAsync());
  }

  public static TheoryData<string, string> InvalidBodies() => new()
  {
    { "SourceEntryId", """{ "targetEntryId": "$T", "relationshipType": "Near" }""" },
    { "TargetEntryId", """{ "sourceEntryId": "$S", "relationshipType": "Near" }""" },
    { "RelationshipType", """{ "sourceEntryId": "$S", "targetEntryId": "$T" }""" },
    { "RelationshipType", """{ "sourceEntryId": "$S", "targetEntryId": "$T", "relationshipType": "   " }""" },
    { "RelationshipType", $$"""{ "sourceEntryId": "$S", "targetEntryId": "$T", "relationshipType": "{{new string('a', 61)}}" }""" },
    { "Description", $$"""{ "sourceEntryId": "$S", "targetEntryId": "$T", "relationshipType": "Near", "description": "{{new string('d', 1001)}}" }""" },
  };

  [Theory]
  [MemberData(nameof(InvalidBodies))]
  public async Task Invalid_bodies_are_400_keyed_by_field(string field, string json)
  {
    var (world, sharn, breland) = await SeedAsync(Factory);
    var body = json.Replace("$S", sharn.Id.ToString()).Replace("$T", breland.Id.ToString());

    var response = await world[Caller.GameMaster].PostAsync(
        $"/api/settings/{world.SettingId}/relationships",
        new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

    await AssertFieldErrorAsync(response, field);
    Assert.Equal(0, await RelationshipCountAsync());
  }

  [Fact]
  public async Task Limits_are_inclusive()
  {
    var (world, sharn, breland) = await SeedAsync(Factory);

    var response = await CreateAsync(world, Caller.GameMaster, new
    {
      sourceEntryId = sharn.Id,
      targetEntryId = breland.Id,
      relationshipType = new string('a', 60),
      description = new string('d', 1000),
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(Caller.Player, HttpStatusCode.Forbidden)]
  public async Task Non_GMs_are_refused_and_nothing_is_created(Caller caller, HttpStatusCode expected)
  {
    var (world, sharn, breland) = await SeedAsync(Factory);

    var response = await CreateAsync(world, caller,
        new { sourceEntryId = sharn.Id, targetEntryId = breland.Id, relationshipType = "Capital of" });

    Assert.Equal(expected, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(0, await RelationshipCountAsync());
  }
}
