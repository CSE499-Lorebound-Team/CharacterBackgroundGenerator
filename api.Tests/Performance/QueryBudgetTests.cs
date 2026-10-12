using System.Net;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Performance;

// P8-04: every read endpoint runs a fixed, small number of SQL commands,
// however many rows it returns (no N+1, no per-item lookups).
[Collection(PostgresCollection.Name)]
public class QueryBudgetTests : PostgresTestBase
{
  private readonly ITestOutputHelper _output;

  public QueryBudgetTests(PostgresFixture fixture, ITestOutputHelper output)
      : base(fixture)
  {
    _output = output;
  }

  private sealed record World(SharingWorld Sharing, SettingEntry Entry, Character Character);

  // n entries (each linked to the first), n characters for the Player (each
  // with a homeland), n invites and n extra settings for the owner.
  private async Task<World> SeedAsync(int n)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var entries = new List<SettingEntry>();
    for (var i = 0; i < n; i++)
    {
      var entry = await world.AddEntryAsync($"Place {i}", description: $"Lore {i}");
      if (entries.Count > 0)
      {
        await world.LinkAsync(entry, entries[0], "Located in");
      }

      entries.Add(entry);
      await world.AddEntryAsync($"Culture {i}", SettingEntryType.Culture);
      var character = await world.AddCharacterAsync(Caller.Player, $"Hero {i}");
      await world.AddChoiceAsync(character, "homeland", entry);
      await world.CreateSettingAsync($"Extra {i}");
    }

    await world.AddInvitesAsync(n);
    var first = await Factory.WithDbAsync(db => db.Characters.FirstAsync(c => c.Name == "Hero 0"));
    return new World(world, entries[0], first);
  }

  private async Task<int> CountAsync(SharingWorld world, Caller caller, string url)
  {
    Factory.Sql.Clear();
    var response = await world[caller].GetAsync(url);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return Factory.Sql.Commands.Count;
  }

  // Ceilings include the 2 queries every signed-in request makes (the
  // security stamp check reloads the user and their claims, P1-01).
  public static TheoryData<string, Caller, int> Endpoints => new()
  {
    { "/api/settings", Caller.Owner, 4 },
    { "/api/settings/{setting}", Caller.Player, 6 },
    { "/api/settings/{setting}/entries", Caller.Player, 5 },
    { "/api/settings/{setting}/entries?search=lore", Caller.Player, 5 },
    { "/api/settings/{setting}/entries/{entry}", Caller.Player, 6 },
    { "/api/settings/{setting}/relationships", Caller.Player, 5 },
    { "/api/settings/{setting}/members", Caller.Player, 5 },
    { "/api/settings/{setting}/invites", Caller.Owner, 5 },
    { "/api/settings/{setting}/characters", Caller.GameMaster, 5 },
    { "/api/characters", Caller.Player, 4 },
    { "/api/characters/{character}", Caller.Player, 8 },
    { "/api/characters/{character}/steps/culture/options", Caller.Player, 6 },
    { "/api/dashboard", Caller.Player, 10 },
    { "/api/dashboard", Caller.Owner, 10 },
  };

  [Theory]
  [MemberData(nameof(Endpoints))]
  public async Task Query_count_is_small_and_does_not_grow_with_the_data(string template, Caller caller, int ceiling)
  {
    var small = await SeedAsync(1);
    var smallCount = await CountAsync(small.Sharing, caller, Url(template, small));
    await Fixture.ResetAsync();
    var large = await SeedAsync(12);
    var largeCount = await CountAsync(large.Sharing, caller, Url(template, large));

    _output.WriteLine($"{template} as {caller}: {smallCount} queries with 1 row, {largeCount} with 12");
    foreach (var sql in Factory.Sql.Commands)
    {
      _output.WriteLine("---\n" + sql);
    }

    Assert.Equal(smallCount, largeCount);
    Assert.True(largeCount <= ceiling, $"{template} ran {largeCount} queries; the ceiling is {ceiling}.");
  }

  private static string Url(string template, World world) => template
      .Replace("{setting}", world.Sharing.SettingId.ToString())
      .Replace("{entry}", world.Entry.Id.ToString())
      .Replace("{character}", world.Character.Id.ToString());
}
