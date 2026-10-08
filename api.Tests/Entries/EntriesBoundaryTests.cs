using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Entries;

/// <summary>
/// P4-07: checks that span every entries endpoint at once, on top of the
/// per-endpoint suites (EntriesList, EntriesCreate, EntriesDetail,
/// EntriesUpdate, EntriesDelete; case-insensitive uniqueness is also in
/// Data/SettingEntryTests). Secret lore must never leak to a Player.
/// </summary>
[Collection(PostgresCollection.Name)]
public class EntriesBoundaryTests : PostgresTestBase
{
  private const string SecretName = "The Lord of Blades";

  public EntriesBoundaryTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  /// <summary>Every entries endpoint for one setting and entry.</summary>
  private static IEnumerable<(HttpMethod Method, string Path)> Endpoints(Guid settingId, Guid entryId) =>
  [
    (HttpMethod.Get, $"/api/settings/{settingId}/entries"),
    (HttpMethod.Post, $"/api/settings/{settingId}/entries"),
    (HttpMethod.Get, $"/api/settings/{settingId}/entries/{entryId}"),
    (HttpMethod.Put, $"/api/settings/{settingId}/entries/{entryId}"),
    (HttpMethod.Delete, $"/api/settings/{settingId}/entries/{entryId}?force=true"),
  ];

  private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path)
  {
    var request = new HttpRequestMessage(method, path);
    if (method == HttpMethod.Post || method == HttpMethod.Put)
    {
      request.Content = JsonContent.Create(new { name = "Renamed", entryType = "Location" });
    }

    return client.SendAsync(request);
  }

  // Everything but traceId, which differs per request by design.
  private static string Comparable(JsonElement problem) =>
      JsonSerializer.Serialize(problem.EnumerateObject()
          .Where(p => p.Name != "traceId")
          .ToDictionary(p => p.Name, p => p.Value.ToString()));

  private Task<List<(string Name, bool IsGmOnly)>> StoredEntriesAsync() =>
      Factory.WithDbAsync(async db => (await db.SettingEntries
              .OrderBy(e => e.Name)
              .Select(e => new { e.Name, e.IsGmOnly })
              .ToListAsync())
          .Select(e => (e.Name, e.IsGmOnly))
          .ToList());

  /// <summary>Sharn (public) linked both ways with a GM-only person, and to public Breland.</summary>
  private static async Task<(SharingWorld World, SettingEntry Sharn, SettingEntry Secret)> SeedSecretAsync(
      CustomWebApplicationFactory factory)
  {
    var world = await SharingWorld.SeedAsync(factory);
    var sharn = await world.AddEntryAsync("Sharn", description: "City of Towers");
    var breland = await world.AddEntryAsync("Breland");
    var secret = await world.AddEntryAsync(SecretName, SettingEntryType.Person, isGmOnly: true,
        description: "Warforged leader hiding under Sharn");
    await world.LinkAsync(sharn, breland, "Capital of");
    await world.LinkAsync(secret, sharn, "Spies on");
    await world.LinkAsync(sharn, secret, "Unknowingly hosts");
    return (world, sharn, secret);
  }

  [Fact]
  public async Task Anonymous_gets_401_from_every_entries_endpoint_and_nothing_changes()
  {
    var (world, sharn, _) = await SeedSecretAsync(Factory);

    foreach (var (method, path) in Endpoints(world.SettingId, sharn.Id))
    {
      var response = await SendAsync(world[Caller.Anonymous], method, path);
      Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{method} {path}: {response.StatusCode}");
      await JsonAssert.ReadProblemAsync(response);
    }

    Assert.Equal(3, (await StoredEntriesAsync()).Count);
  }

  [Fact]
  public async Task Non_member_gets_the_same_404_for_a_real_and_a_missing_setting_everywhere()
  {
    var (world, sharn, _) = await SeedSecretAsync(Factory);

    var real = Endpoints(world.SettingId, sharn.Id).ToList();
    var missing = Endpoints(Guid.NewGuid(), sharn.Id).ToList();

    for (var i = 0; i < real.Count; i++)
    {
      var realResponse = await SendAsync(world[Caller.NonMember], real[i].Method, real[i].Path);
      var missingResponse = await SendAsync(world[Caller.NonMember], missing[i].Method, missing[i].Path);

      var label = $"{real[i].Method} {real[i].Path}";
      Assert.True(realResponse.StatusCode == HttpStatusCode.NotFound, $"{label}: {realResponse.StatusCode}");
      Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
      Assert.Equal(
          Comparable(await JsonAssert.ReadProblemAsync(missingResponse)),
          Comparable(await JsonAssert.ReadProblemAsync(realResponse)));
    }

    Assert.Equal(3, (await StoredEntriesAsync()).Count);
  }

  /// <summary>
  /// A Player is refused writes before the entry is looked up, so the answer
  /// is the same 403 for a public, a hidden and a missing entry.
  /// </summary>
  [Fact]
  public async Task Player_writes_are_403_whether_the_entry_is_public_hidden_or_missing()
  {
    var (world, sharn, secret) = await SeedSecretAsync(Factory);
    var player = world[Caller.Player];

    var post = await SendAsync(player, HttpMethod.Post, $"/api/settings/{world.SettingId}/entries");
    Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);

    foreach (var entryId in new[] { sharn.Id, secret.Id, Guid.NewGuid() })
    {
      foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete })
      {
        var response = await SendAsync(player, method, $"/api/settings/{world.SettingId}/entries/{entryId}?force=true");
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {entryId}: {response.StatusCode}");
      }
    }

    Assert.Equal(
        [("Breland", false), ("Sharn", false), (SecretName, true)],
        await StoredEntriesAsync());
  }

  /// <summary>
  /// Every read a Player can make, collected and searched for any trace of
  /// the hidden entry: its id, name or description.
  /// </summary>
  [Fact]
  public async Task No_player_response_mentions_a_GM_only_entry()
  {
    var (world, sharn, secret) = await SeedSecretAsync(Factory);
    var player = world[Caller.Player];
    var entries = $"/api/settings/{world.SettingId}/entries";
    var bodies = new List<(string Label, string Body)>();

    async Task Record(string path)
    {
      var response = await player.GetAsync(path);
      Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode}");
      bodies.Add((path, await response.Content.ReadAsStringAsync()));
    }

    await Record(entries);
    await Record($"{entries}?type=Person");
    await Record($"{entries}?search=lord");
    await Record($"{entries}?search=warforged");
    await Record($"{entries}?gmOnly=true");
    await Record($"{entries}/{sharn.Id}");
    await Record($"/api/settings/{world.SettingId}");
    await Record("/api/settings");

    Assert.All(bodies, b =>
    {
      Assert.False(b.Body.Contains(secret.Id.ToString(), StringComparison.OrdinalIgnoreCase), $"{b.Label} has the id: {b.Body}");
      Assert.False(b.Body.Contains("Lord of Blades", StringComparison.OrdinalIgnoreCase), $"{b.Label} has the name: {b.Body}");
      Assert.False(b.Body.Contains("Warforged", StringComparison.OrdinalIgnoreCase), $"{b.Label} has the description: {b.Body}");
      Assert.False(b.Body.Contains("isGmOnly", StringComparison.OrdinalIgnoreCase), $"{b.Label} has the flag: {b.Body}");
    });

    var hidden = await player.GetAsync($"{entries}/{secret.Id}");
    var missing = await player.GetAsync($"{entries}/{Guid.NewGuid()}");
    Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    Assert.Equal(
        Comparable(await JsonAssert.ReadProblemAsync(missing)),
        Comparable(await JsonAssert.ReadProblemAsync(hidden)));
  }

  [Fact]
  public async Task Counts_exclude_hidden_entries_and_their_links_for_players_only()
  {
    var (world, _, _) = await SeedSecretAsync(Factory);

    async Task<JsonElement> Get(Caller caller, string path) =>
        await JsonAssert.ReadJsonAsync(await world[caller].GetAsync(path));

    int SharnLinks(JsonElement page) =>
        page.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Sharn")
            .GetProperty("relationshipCount").GetInt32();

    int ListedEntryCount(JsonElement page) =>
        page.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == world.SettingId)
            .GetProperty("entryCount").GetInt32();

    var entries = $"/api/settings/{world.SettingId}/entries";
    var detail = $"/api/settings/{world.SettingId}";

    // Player: Sharn's only visible link is Breland; Person count disappears.
    Assert.Equal(1, SharnLinks(await Get(Caller.Player, entries)));
    Assert.Equal(2, ListedEntryCount(await Get(Caller.Player, "/api/settings")));
    var playerCounts = (await Get(Caller.Player, detail)).GetProperty("entryCountsByType");
    Assert.Equal(2, playerCounts.GetProperty("Location").GetInt32());
    Assert.False(playerCounts.TryGetProperty("Person", out _));

    foreach (var gm in new[] { Caller.GameMaster, Caller.Owner })
    {
      Assert.Equal(3, SharnLinks(await Get(gm, entries)));
      Assert.Equal(3, ListedEntryCount(await Get(gm, "/api/settings")));
      Assert.Equal(1, (await Get(gm, detail)).GetProperty("entryCountsByType").GetProperty("Person").GetInt32());
    }
  }

  [Fact]
  public async Task An_entry_hidden_after_the_fact_disappears_for_players_at_once()
  {
    var (world, sharn, _) = await SeedSecretAsync(Factory);
    var path = $"/api/settings/{world.SettingId}/entries/{sharn.Id}";

    var hide = await world[Caller.GameMaster].PutAsJsonAsync(path,
        new { name = "Sharn", entryType = "Location", isGmOnly = true });
    Assert.Equal(HttpStatusCode.OK, hide.StatusCode);

    Assert.Equal(HttpStatusCode.NotFound, (await world[Caller.Player].GetAsync(path)).StatusCode);
    var list = await JsonAssert.ReadJsonAsync(
        await world[Caller.Player].GetAsync($"/api/settings/{world.SettingId}/entries"));
    var breland = Assert.Single(list.GetProperty("items").EnumerateArray());
    Assert.Equal("Breland", breland.GetProperty("name").GetString());
    Assert.Equal(0, breland.GetProperty("relationshipCount").GetInt32());
  }
}
