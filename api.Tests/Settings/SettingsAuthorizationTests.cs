using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Tests.Settings;

/// <summary>
/// P2-09: the permission matrix for every /api/settings endpoint, through
/// HTTP with real cookies. GameMaster and Player rows are inserted directly
/// until invites exist (Phase 3).
/// </summary>
[Collection(PostgresCollection.Name)]
public class SettingsAuthorizationTests : PostgresTestBase
{
  public SettingsAuthorizationTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  public enum Caller
  {
    Anonymous,
    NonMember,
    Player,
    GameMaster,
    Owner,
  }

  private sealed record World(Guid SettingId, Dictionary<Caller, HttpClient> Clients)
  {
    public HttpClient this[Caller caller] => Clients[caller];
  }

  /// <summary>
  /// One setting created by the owner through the API, plus a GameMaster
  /// (not the owner), a Player, a signed-in non-member and an anonymous client.
  /// </summary>
  private async Task<World> SeedAsync()
  {
    var clients = new Dictionary<Caller, HttpClient>
    {
      [Caller.Anonymous] = Factory.CreateCookieClient(),
    };
    var userIds = new Dictionary<Caller, Guid>();

    foreach (var caller in new[] { Caller.Owner, Caller.GameMaster, Caller.Player, Caller.NonMember })
    {
      var (client, user) = await Factory.CreateSignedInClientAsync(
          $"{caller}@example.com".ToLowerInvariant(), caller.ToString());
      clients[caller] = client;
      userIds[caller] = user.Id;
    }

    var created = await clients[Caller.Owner].PostAsJsonAsync(
        "/api/settings", new { name = "Osepia", description = "Original" });
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var settingId = (await JsonAssert.ReadJsonAsync(created)).GetProperty("id").GetGuid();

    await Factory.WithDbAsync(db =>
    {
      db.SettingMemberships.AddRange(
          Membership(settingId, userIds[Caller.GameMaster], SettingRole.GameMaster),
          Membership(settingId, userIds[Caller.Player], SettingRole.Player));
      return db.SaveChangesAsync();
    });

    return new World(settingId, clients);
  }

  private static SettingMembership Membership(Guid settingId, Guid userId, SettingRole role) =>
      new() { Id = Guid.NewGuid(), CampaignSettingId = settingId, UserId = userId, Role = role };

  private Task<string> SettingNameAsync(Guid id) =>
      Factory.WithDbAsync(db => db.CampaignSettings
          .Where(s => s.Id == id)
          .Select(s => s.Name)
          .SingleAsync());

  // ---- create ----

  [Fact]
  public async Task Create_anonymous_is_401()
  {
    var response = await Factory.CreateCookieClient()
        .PostAsJsonAsync("/api/settings", new { name = "Osepia" });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(0, await Factory.WithDbAsync(db => db.CampaignSettings.CountAsync()));
  }

  [Fact]
  public async Task Create_makes_the_caller_owner_and_GameMaster()
  {
    var (client, user) = await Factory.CreateSignedInClientAsync();

    var response = await client.PostAsJsonAsync("/api/settings", new { name = "Osepia" });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    var id = body.GetProperty("id").GetGuid();
    Assert.Equal($"/api/settings/{id}", response.Headers.Location?.AbsolutePath);
    Assert.Equal("GameMaster", body.GetProperty("myRole").GetString());
    Assert.True(body.GetProperty("isOwner").GetBoolean());

    var membership = await Factory.WithDbAsync(db => db.SettingMemberships.SingleAsync());
    Assert.Equal((id, user.Id, SettingRole.GameMaster),
        (membership.CampaignSettingId, membership.UserId, membership.Role));
    Assert.Equal(user.Id, await Factory.WithDbAsync(db =>
        db.CampaignSettings.Select(s => s.OwnerUserId).SingleAsync()));
  }

  // ---- detail ----

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized, null, null)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound, null, null)]
  [InlineData(Caller.Player, HttpStatusCode.OK, "Player", false)]
  [InlineData(Caller.GameMaster, HttpStatusCode.OK, "GameMaster", false)]
  [InlineData(Caller.Owner, HttpStatusCode.OK, "GameMaster", true)]
  public async Task Detail(Caller caller, HttpStatusCode expected, string? myRole, bool? isOwner)
  {
    var world = await SeedAsync();

    var response = await world[caller].GetAsync($"/api/settings/{world.SettingId}");

    Assert.Equal(expected, response.StatusCode);
    if (expected == HttpStatusCode.OK)
    {
      var body = await JsonAssert.ReadJsonAsync(response);
      Assert.Equal(myRole, body.GetProperty("myRole").GetString());
      Assert.Equal(isOwner, body.GetProperty("isOwner").GetBoolean());
      Assert.Equal(3, body.GetProperty("memberCount").GetInt32());
      Assert.Equal("Owner", body.GetProperty("ownerDisplayName").GetString());
    }
    else
    {
      await JsonAssert.ReadProblemAsync(response);
    }
  }

  // ---- update ----

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(Caller.Player, HttpStatusCode.Forbidden)]
  [InlineData(Caller.GameMaster, HttpStatusCode.OK)]
  [InlineData(Caller.Owner, HttpStatusCode.OK)]
  public async Task Update(Caller caller, HttpStatusCode expected)
  {
    var world = await SeedAsync();

    var response = await world[caller].PutAsJsonAsync(
        $"/api/settings/{world.SettingId}", new { name = "Renamed", description = "New" });

    Assert.Equal(expected, response.StatusCode);
    if (expected == HttpStatusCode.OK)
    {
      var body = await JsonAssert.ReadJsonAsync(response);
      Assert.Equal("Renamed", body.GetProperty("name").GetString());
      Assert.Equal("Renamed", await SettingNameAsync(world.SettingId));
    }
    else
    {
      await JsonAssert.ReadProblemAsync(response);
      Assert.Equal("Osepia", await SettingNameAsync(world.SettingId));
    }
  }

  // ---- delete ----

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(Caller.Player, HttpStatusCode.Forbidden)]
  [InlineData(Caller.GameMaster, HttpStatusCode.Forbidden)]
  [InlineData(Caller.Owner, HttpStatusCode.NoContent)]
  public async Task Delete(Caller caller, HttpStatusCode expected)
  {
    var world = await SeedAsync();

    var response = await world[caller].DeleteAsync($"/api/settings/{world.SettingId}");

    Assert.Equal(expected, response.StatusCode);
    var stillThere = await Factory.WithDbAsync(db =>
        db.CampaignSettings.AnyAsync(s => s.Id == world.SettingId));
    Assert.Equal(expected != HttpStatusCode.NoContent, stillThere);
    if (expected != HttpStatusCode.NoContent)
    {
      await JsonAssert.ReadProblemAsync(response);
    }
  }

  // ---- list ----

  [Theory]
  [InlineData(Caller.NonMember, false, null, null)]
  [InlineData(Caller.Player, true, "Player", false)]
  [InlineData(Caller.GameMaster, true, "GameMaster", false)]
  [InlineData(Caller.Owner, true, "GameMaster", true)]
  public async Task List_shows_the_setting_only_to_members(
      Caller caller, bool listed, string? myRole, bool? isOwner)
  {
    var world = await SeedAsync();

    var response = await world[caller].GetAsync("/api/settings");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    var items = body.GetProperty("items").EnumerateArray().ToList();
    Assert.Equal(listed ? 1 : 0, body.GetProperty("totalCount").GetInt32());
    Assert.Equal(listed ? 1 : 0, items.Count);
    if (listed)
    {
      Assert.Equal(world.SettingId, items[0].GetProperty("id").GetGuid());
      Assert.Equal(myRole, items[0].GetProperty("myRole").GetString());
      Assert.Equal(isOwner, items[0].GetProperty("isOwner").GetBoolean());
    }
  }

  [Fact]
  public async Task List_anonymous_is_401()
  {
    var response = await Factory.CreateCookieClient().GetAsync("/api/settings");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
  }

  // ---- missing setting ----

  [Fact]
  public async Task Unknown_setting_id_is_404_for_detail_update_and_delete()
  {
    var (client, _) = await Factory.CreateSignedInClientAsync();
    var url = $"/api/settings/{Guid.NewGuid()}";

    var detail = await client.GetAsync(url);
    var update = await client.PutAsJsonAsync(url, new { name = "X" });
    var delete = await client.DeleteAsync(url);

    Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
  }

  // Non-members must not be able to tell "exists" from "does not exist".
  [Fact]
  public async Task Non_member_sees_the_same_404_as_for_a_missing_setting()
  {
    var world = await SeedAsync();

    var existing = await JsonAssert.ReadProblemAsync(
        await world[Caller.NonMember].GetAsync($"/api/settings/{world.SettingId}"));
    var missing = await JsonAssert.ReadProblemAsync(
        await world[Caller.NonMember].GetAsync($"/api/settings/{Guid.NewGuid()}"));

    Assert.Equal(missing.GetProperty("title").GetString(), existing.GetProperty("title").GetString());
    Assert.Equal(missing.GetProperty("detail").GetString(), existing.GetProperty("detail").GetString());
  }
}
