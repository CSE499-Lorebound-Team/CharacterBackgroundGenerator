using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Relationships;

// P5-05 (ADR 0002): suggested types plus free text, readable labels,
// compared ignoring case.
public class RelationshipTypesNormalizeTests
{
  [Theory]
  [InlineData("  Spies   on  ", "Spies on")]
  [InlineData("located IN", "Located in")]
  [InlineData(" MEMBER\tof ", "Member of")]
  [InlineData("Sworn rival of", "Sworn rival of")]
  public void Normalize_trims_collapses_spaces_and_adopts_suggested_spelling(string input, string expected)
  {
    Assert.Equal(expected, RelationshipTypes.Normalize(input));
  }

  [Fact]
  public void Suggested_types_are_already_normal_and_fit_the_limit()
  {
    Assert.All(RelationshipTypes.Suggested, type =>
    {
      Assert.Equal(type, RelationshipTypes.Normalize(type));
      Assert.InRange(type.Length, 1, RelationshipTypes.MaxLength);
    });
    Assert.Equal(
        RelationshipTypes.Suggested.Count,
        RelationshipTypes.Suggested.Distinct(StringComparer.OrdinalIgnoreCase).Count());
  }
}

[Collection(PostgresCollection.Name)]
public class RelationshipTypesEndpointTests : PostgresTestBase
{
  public RelationshipTypesEndpointTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  [Fact]
  public async Task Signed_in_users_get_the_suggested_list()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[Caller.NonMember].GetAsync("/api/relationship-types");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(response, "suggested");
    Assert.Equal(
        RelationshipTypes.Suggested,
        body.GetProperty("suggested").EnumerateArray().Select(type => type.GetString()!));
  }

  [Fact]
  public async Task Anonymous_gets_401()
  {
    var response = await Factory.CreateCookieClient().GetAsync("/api/relationship-types");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Create_stores_the_suggested_spelling_and_rejects_a_case_duplicate()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var breland = await world.AddEntryAsync("Breland");
    var path = $"/api/settings/{world.SettingId}/relationships";

    var first = await world[Caller.GameMaster].PostAsJsonAsync(path,
        new { sourceEntryId = sharn.Id, targetEntryId = breland.Id, relationshipType = " located  IN " });
    var again = await world[Caller.GameMaster].PostAsJsonAsync(path,
        new { sourceEntryId = sharn.Id, targetEntryId = breland.Id, relationshipType = "LOCATED IN" });
    var custom = await world[Caller.GameMaster].PostAsJsonAsync(path,
        new { sourceEntryId = sharn.Id, targetEntryId = breland.Id, relationshipType = "sworn RIVAL of" });
    var customAgain = await world[Caller.GameMaster].PostAsJsonAsync(path,
        new { sourceEntryId = sharn.Id, targetEntryId = breland.Id, relationshipType = "Sworn rival of" });

    Assert.Equal(HttpStatusCode.Created, first.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    Assert.Equal(HttpStatusCode.Created, custom.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, customAgain.StatusCode);

    var stored = await Factory.WithDbAsync(db => db.SettingEntryRelationships
        .Select(r => r.RelationshipType)
        .OrderBy(type => type)
        .ToListAsync());
    Assert.Equal(["Located in", "sworn RIVAL of"], stored);
  }
}
