using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Settings;

/// <summary>
/// P2-09: GET /api/settings lists only the caller's settings, and the role
/// filter splits them by the caller's role.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SettingsListTests : PostgresTestBase
{
  public SettingsListTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private HttpClient _me = null!;

  /// <summary>
  /// I own "Mine", play in "Playing", GM "Running" (owned by someone else)
  /// and have nothing to do with "Stranger". Created in that order, so the
  /// newest-first list is Running, Playing, Mine.
  /// </summary>
  private async Task SeedAsync()
  {
    var (me, meUser) = await Factory.CreateSignedInClientAsync("me@example.com", "Me");
    var (other, _) = await Factory.CreateSignedInClientAsync("other@example.com", "Other");
    _me = me;

    await CreateAsync(me, "Mine");
    var playing = await CreateAsync(other, "Playing");
    var running = await CreateAsync(other, "Running");
    await CreateAsync(other, "Stranger");

    await Factory.WithDbAsync(db =>
    {
      db.SettingMemberships.AddRange(
          new SettingMembership { Id = Guid.NewGuid(), CampaignSettingId = playing, UserId = meUser.Id, Role = SettingRole.Player },
          new SettingMembership { Id = Guid.NewGuid(), CampaignSettingId = running, UserId = meUser.Id, Role = SettingRole.GameMaster });
      return db.SaveChangesAsync();
    });
  }

  private static async Task<Guid> CreateAsync(HttpClient client, string name)
  {
    var response = await client.PostAsJsonAsync("/api/settings", new { name });
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return (await JsonAssert.ReadJsonAsync(response)).GetProperty("id").GetGuid();
  }

  private async Task<(List<string> Names, int TotalCount)> ListAsync(string query = "")
  {
    var response = await _me.GetAsync("/api/settings" + query);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    var names = body.GetProperty("items").EnumerateArray()
        .Select(item => item.GetProperty("name").GetString()!)
        .ToList();
    return (names, body.GetProperty("totalCount").GetInt32());
  }

  [Fact]
  public async Task Lists_only_settings_I_belong_to_newest_first()
  {
    await SeedAsync();

    var (names, total) = await ListAsync();

    Assert.Equal(["Running", "Playing", "Mine"], names);
    Assert.Equal(3, total);
  }

  [Theory]
  [InlineData("gm", new[] { "Running", "Mine" })]
  [InlineData("GM", new[] { "Running", "Mine" })]
  [InlineData("player", new[] { "Playing" })]
  public async Task Role_filter_splits_by_my_role(string role, string[] expected)
  {
    await SeedAsync();

    var (names, total) = await ListAsync($"?role={role}");

    Assert.Equal(expected, names);
    Assert.Equal(expected.Length, total);
  }

  [Fact]
  public async Task Unknown_role_filter_is_400()
  {
    await SeedAsync();

    var response = await _me.GetAsync("/api/settings?role=owner");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Paging_counts_only_my_settings()
  {
    await SeedAsync();

    var first = await ListAsync("?page=1&pageSize=2");
    var second = await ListAsync("?page=2&pageSize=2");

    Assert.Equal(["Running", "Playing"], first.Names);
    Assert.Equal(["Mine"], second.Names);
    Assert.Equal(3, first.TotalCount);
    Assert.Equal(3, second.TotalCount);
  }

  [Fact]
  public async Task Search_cannot_reach_settings_I_do_not_belong_to()
  {
    await SeedAsync();

    var (names, total) = await ListAsync("?search=Stranger");

    Assert.Empty(names);
    Assert.Equal(0, total);
  }
}
