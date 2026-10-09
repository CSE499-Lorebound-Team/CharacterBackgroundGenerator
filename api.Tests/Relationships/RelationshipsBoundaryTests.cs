using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Relationships;

/// <summary>
/// P5-06: checks that span every relationship endpoint at once, on top of
/// the per-endpoint suites (RelationshipsList, RelationshipsCreate,
/// RelationshipsUpdateDelete, RelationshipTypes; storage rules in
/// Data/SettingEntryRelationshipTests). A link must never reveal a GM-only
/// entry to a Player, and one setting's links never reach another's.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RelationshipsBoundaryTests : PostgresTestBase
{
  private const string SecretName = "The Lord of Blades";

  public RelationshipsBoundaryTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private sealed record World(
      SharingWorld Sharing,
      SettingEntry Sharn,
      SettingEntry Breland,
      SettingEntry Secret,
      SettingEntryRelationship Public,
      SettingEntryRelationship SpiesOn,
      SettingEntryRelationship Hosts);

  /// <summary>Sharn -Capital of-> Breland, plus links both ways with a GM-only person.</summary>
  private async Task<World> SeedAsync()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn", description: "City of Towers");
    var breland = await world.AddEntryAsync("Breland");
    var secret = await world.AddEntryAsync(SecretName, SettingEntryType.Person, isGmOnly: true,
        description: "Warforged leader hiding under Sharn");

    return new World(
        world,
        sharn,
        breland,
        secret,
        await world.LinkAsync(sharn, breland, "Capital of"),
        await world.LinkAsync(secret, sharn, "Spies on", "Through the warforged"),
        await world.LinkAsync(sharn, secret, "Unknowingly hosts"));
  }

  private static string Relationships(Guid settingId) => $"/api/settings/{settingId}/relationships";

  /// <summary>Every relationship endpoint of one setting, for one relationship.</summary>
  private static IEnumerable<(HttpMethod Method, string Path)> Endpoints(Guid settingId, Guid relationshipId) =>
  [
    (HttpMethod.Get, Relationships(settingId)),
    (HttpMethod.Post, Relationships(settingId)),
    (HttpMethod.Put, $"{Relationships(settingId)}/{relationshipId}"),
    (HttpMethod.Delete, $"{Relationships(settingId)}/{relationshipId}"),
  ];

  private static Task<HttpResponseMessage> SendAsync(
      HttpClient client, HttpMethod method, string path, World world)
  {
    var request = new HttpRequestMessage(method, path);
    if (method == HttpMethod.Post)
    {
      request.Content = JsonContent.Create(new
      {
        sourceEntryId = world.Breland.Id,
        targetEntryId = world.Sharn.Id,
        relationshipType = "Home of",
      });
    }
    else if (method == HttpMethod.Put)
    {
      request.Content = JsonContent.Create(new { relationshipType = "Renamed" });
    }

    return client.SendAsync(request);
  }

  // Everything but traceId, which differs per request by design.
  private static string Comparable(JsonElement problem) =>
      JsonSerializer.Serialize(problem.EnumerateObject()
          .Where(p => p.Name != "traceId")
          .ToDictionary(p => p.Name, p => p.Value.ToString()));

  private Task<List<string>> StoredTypesAsync() =>
      Factory.WithDbAsync(db => db.SettingEntryRelationships
          .OrderBy(r => r.RelationshipType)
          .Select(r => r.RelationshipType)
          .ToListAsync());

  private static readonly List<string> SeededTypes = ["Capital of", "Spies on", "Unknowingly hosts"];

  [Fact]
  public async Task Anonymous_gets_401_from_every_relationship_endpoint_and_nothing_changes()
  {
    var world = await SeedAsync();
    var endpoints = Endpoints(world.Sharing.SettingId, world.Public.Id)
        .Append((HttpMethod.Get, "/api/relationship-types"));

    foreach (var (method, path) in endpoints)
    {
      var response = await SendAsync(world.Sharing[Caller.Anonymous], method, path, world);
      Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{method} {path}: {response.StatusCode}");
      await JsonAssert.ReadProblemAsync(response);
    }

    Assert.Equal(SeededTypes, await StoredTypesAsync());
  }

  [Fact]
  public async Task Non_member_gets_the_same_404_for_a_real_and_a_missing_setting_everywhere()
  {
    var world = await SeedAsync();

    var real = Endpoints(world.Sharing.SettingId, world.Public.Id).ToList();
    var missing = Endpoints(Guid.NewGuid(), world.Public.Id).ToList();

    for (var i = 0; i < real.Count; i++)
    {
      var realResponse = await SendAsync(world.Sharing[Caller.NonMember], real[i].Method, real[i].Path, world);
      var missingResponse = await SendAsync(world.Sharing[Caller.NonMember], missing[i].Method, missing[i].Path, world);

      var label = $"{real[i].Method} {real[i].Path}";
      Assert.True(realResponse.StatusCode == HttpStatusCode.NotFound, $"{label}: {realResponse.StatusCode}");
      Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
      Assert.Equal(
          Comparable(await JsonAssert.ReadProblemAsync(missingResponse)),
          Comparable(await JsonAssert.ReadProblemAsync(realResponse)));
    }

    Assert.Equal(SeededTypes, await StoredTypesAsync());
  }

  /// <summary>
  /// A Player is refused writes before the relationship is looked up, so the
  /// answer is the same 403 for a public link, one to a hidden entry and a
  /// missing one: a write cannot be used to probe for hidden links.
  /// </summary>
  [Fact]
  public async Task Player_writes_are_403_whether_the_link_is_public_hidden_or_missing()
  {
    var world = await SeedAsync();
    var player = world.Sharing[Caller.Player];
    var settingId = world.Sharing.SettingId;
    var problems = new List<string>();

    var post = await SendAsync(player, HttpMethod.Post, Relationships(settingId), world);
    Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
    problems.Add(Comparable(await JsonAssert.ReadProblemAsync(post)));

    foreach (var relationshipId in new[] { world.Public.Id, world.SpiesOn.Id, Guid.NewGuid() })
    {
      foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete })
      {
        var response = await SendAsync(player, method, $"{Relationships(settingId)}/{relationshipId}", world);
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {relationshipId}: {response.StatusCode}");
        problems.Add(Comparable(await JsonAssert.ReadProblemAsync(response)));
      }
    }

    Assert.Single(problems.Distinct());
    Assert.Equal(SeededTypes, await StoredTypesAsync());
  }

  /// <summary>
  /// Every read a Player can make that touches relationships, collected and
  /// searched for any trace of the hidden entry or of the links to it.
  /// </summary>
  [Fact]
  public async Task No_player_response_mentions_a_GM_only_entry_or_its_links()
  {
    var world = await SeedAsync();
    var player = world.Sharing[Caller.Player];
    var settingId = world.Sharing.SettingId;
    var bodies = new List<(string Label, string Body)>();

    async Task Record(string path)
    {
      var response = await player.GetAsync(path);
      Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode}");
      bodies.Add((path, await response.Content.ReadAsStringAsync()));
    }

    await Record(Relationships(settingId));
    await Record($"{Relationships(settingId)}?entryId={world.Sharn.Id}");
    await Record($"{Relationships(settingId)}?type=Spies%20on");
    await Record($"{Relationships(settingId)}?type=unknowingly%20hosts");
    await Record($"/api/settings/{settingId}/entries");
    await Record($"/api/settings/{settingId}/entries/{world.Sharn.Id}");

    var forbidden = new[]
    {
      world.Secret.Id.ToString(),
      world.SpiesOn.Id.ToString(),
      world.Hosts.Id.ToString(),
      "Lord of Blades",
      "Warforged",
      "Spies on",
      "Unknowingly hosts",
    };

    Assert.All(bodies, b => Assert.All(forbidden, text =>
        Assert.False(b.Body.Contains(text, StringComparison.OrdinalIgnoreCase), $"{b.Label} has '{text}': {b.Body}")));

    // Filtering by the hidden entry is the same 404 as by a missing one.
    var hidden = await player.GetAsync($"{Relationships(settingId)}?entryId={world.Secret.Id}");
    var missing = await player.GetAsync($"{Relationships(settingId)}?entryId={Guid.NewGuid()}");
    Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    Assert.Equal(
        Comparable(await JsonAssert.ReadProblemAsync(missing)),
        Comparable(await JsonAssert.ReadProblemAsync(hidden)));
  }

  [Fact]
  public async Task GameMasters_see_every_link_in_list_and_entry_detail()
  {
    var world = await SeedAsync();

    foreach (var gm in new[] { Caller.GameMaster, Caller.Owner })
    {
      var list = await JsonAssert.ReadJsonAsync(
          await world.Sharing[gm].GetAsync(Relationships(world.Sharing.SettingId)));
      var detail = await JsonAssert.ReadJsonAsync(await world.Sharing[gm].GetAsync(
          $"/api/settings/{world.Sharing.SettingId}/entries/{world.Sharn.Id}"));

      Assert.Equal(3, list.GetProperty("totalCount").GetInt32());
      Assert.Equal(2, detail.GetProperty("outgoing").GetArrayLength());
      Assert.Equal(1, detail.GetProperty("incoming").GetArrayLength());
    }
  }

  [Fact]
  public async Task Hiding_and_unhiding_an_entry_hides_and_restores_its_links_for_players_at_once()
  {
    var world = await SeedAsync();
    var settingId = world.Sharing.SettingId;
    var brelandPath = $"/api/settings/{settingId}/entries/{world.Breland.Id}";

    async Task<int> PlayerLinkCount() =>
        (await JsonAssert.ReadJsonAsync(await world.Sharing[Caller.Player].GetAsync(
            $"{Relationships(settingId)}?entryId={world.Sharn.Id}")))
        .GetProperty("totalCount").GetInt32();

    Assert.Equal(1, await PlayerLinkCount());

    var hide = await world.Sharing[Caller.GameMaster].PutAsJsonAsync(brelandPath,
        new { name = "Breland", entryType = "Location", isGmOnly = true });
    Assert.Equal(HttpStatusCode.OK, hide.StatusCode);
    Assert.Equal(0, await PlayerLinkCount());

    var unhide = await world.Sharing[Caller.GameMaster].PutAsJsonAsync(brelandPath,
        new { name = "Breland", entryType = "Location", isGmOnly = false });
    Assert.Equal(HttpStatusCode.OK, unhide.StatusCode);
    Assert.Equal(1, await PlayerLinkCount());
  }

  [Fact]
  public async Task Force_deleting_an_entry_removes_its_links_in_both_directions_and_keeps_the_rest()
  {
    var world = await SeedAsync();
    var settingId = world.Sharing.SettingId;
    var gm = world.Sharing[Caller.GameMaster];
    var aundair = await world.Sharing.AddEntryAsync("Aundair");
    await world.Sharing.LinkAsync(aundair, world.Breland, "Borders");

    var refused = await gm.DeleteAsync($"/api/settings/{settingId}/entries/{world.Sharn.Id}");
    Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    Assert.Equal(4, (await StoredTypesAsync()).Count);

    var forced = await gm.DeleteAsync($"/api/settings/{settingId}/entries/{world.Sharn.Id}?force=true");
    Assert.Equal(HttpStatusCode.NoContent, forced.StatusCode);

    var list = await JsonAssert.ReadJsonAsync(await gm.GetAsync(Relationships(settingId)));
    var remaining = Assert.Single(list.GetProperty("items").EnumerateArray());
    Assert.Equal("Borders", remaining.GetProperty("relationshipType").GetString());
    Assert.Equal(["Borders"], await StoredTypesAsync());

    // The deleted links are gone for good: editing one is 404.
    var put = await gm.PutAsJsonAsync($"{Relationships(settingId)}/{world.Public.Id}", new { relationshipType = "Renamed" });
    Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
  }

  /// <summary>
  /// The Osepia GameMaster is not a member of Krynn. Through Osepia's URLs
  /// they cannot link to, filter by, edit or delete anything of Krynn, and
  /// every refusal looks like a missing id.
  /// </summary>
  [Fact]
  public async Task Another_settings_entries_and_links_cannot_be_reached_through_this_setting()
  {
    var world = await SeedAsync();
    var settingId = world.Sharing.SettingId;
    var gm = world.Sharing[Caller.GameMaster];
    var krynnId = await world.Sharing.CreateSettingAsync("Krynn");
    var palanthas = await world.Sharing.AddEntryAsync("Palanthas", settingId: krynnId);
    var solamnia = await world.Sharing.AddEntryAsync("Solamnia", settingId: krynnId);
    var krynnLink = await world.Sharing.LinkAsync(palanthas, solamnia, "Located in");

    var link = await gm.PostAsJsonAsync(Relationships(settingId),
        new { sourceEntryId = world.Sharn.Id, targetEntryId = palanthas.Id, relationshipType = "Trades with" });
    Assert.Equal(HttpStatusCode.BadRequest, link.StatusCode);

    var filter = await gm.GetAsync($"{Relationships(settingId)}?entryId={palanthas.Id}");
    var filterMissing = await gm.GetAsync($"{Relationships(settingId)}?entryId={Guid.NewGuid()}");
    Assert.Equal(HttpStatusCode.NotFound, filter.StatusCode);
    Assert.Equal(
        Comparable(await JsonAssert.ReadProblemAsync(filterMissing)),
        Comparable(await JsonAssert.ReadProblemAsync(filter)));

    foreach (var (method, path) in new[]
    {
      (HttpMethod.Put, $"{Relationships(settingId)}/{krynnLink.Id}"),
      (HttpMethod.Delete, $"{Relationships(settingId)}/{krynnLink.Id}"),
    })
    {
      var response = await SendAsync(gm, method, path, world);
      var missing = await SendAsync(gm, method, $"{Relationships(settingId)}/{Guid.NewGuid()}", world);
      Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{method}: {response.StatusCode}");
      Assert.Equal(
          Comparable(await JsonAssert.ReadProblemAsync(missing)),
          Comparable(await JsonAssert.ReadProblemAsync(response)));
    }

    var list = await JsonAssert.ReadJsonAsync(await gm.GetAsync(Relationships(settingId)));
    Assert.DoesNotContain(krynnLink.Id.ToString(), list.ToString());

    var stored = await Factory.WithDbAsync(db => db.SettingEntryRelationships.SingleAsync(r => r.Id == krynnLink.Id));
    Assert.Equal((krynnId, "Located in"), (stored.CampaignSettingId, stored.RelationshipType));
  }
}
