using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lorebound.Api.Data;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.Data;

// P8-02: Development seed data.
[Collection(PostgresCollection.Name)]
public class DevelopmentSeederTests : PostgresTestBase
{
  public DevelopmentSeederTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private async Task SeedAsync()
  {
    using var scope = Factory.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync();
  }

  private Task<int[]> RowCountsAsync() =>
      Factory.WithDbAsync(async db => new[]
      {
        await db.Users.CountAsync(),
        await db.CampaignSettings.CountAsync(),
        await db.SettingMemberships.CountAsync(),
        await db.SettingEntries.CountAsync(),
        await db.SettingEntryRelationships.CountAsync(),
        await db.Characters.CountAsync(),
        await db.CharacterChoices.CountAsync(),
      });

  private async Task<HttpClient> SignInAsync(string email, string password)
  {
    var client = Factory.CreateCookieClient();
    var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return client;
  }

  private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
  {
    var response = await client.GetAsync(url);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static List<string> Names(JsonElement page) =>
      page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToList();

  [Fact]
  public async Task Seeds_the_documented_world()
  {
    await SeedAsync();

    Assert.Equal(new[] { 2, 1, 2, 9, 7, 1, 1 }, await RowCountsAsync());
    var gmOnly = await Factory.WithDbAsync(db =>
        db.SettingEntries.Where(e => e.IsGmOnly).Select(e => e.Name).ToListAsync());
    Assert.Equal(["Cult of Beléna"], gmOnly);
  }

  [Fact]
  public async Task Running_it_twice_adds_nothing()
  {
    await SeedAsync();
    var first = await RowCountsAsync();

    await SeedAsync();

    Assert.Equal(first, await RowCountsAsync());
  }

  [Fact]
  public async Task Running_it_again_restores_missing_rows_and_keeps_changed_ones()
  {
    await SeedAsync();
    var full = await RowCountsAsync();
    await Factory.WithDbAsync(async db =>
    {
      await db.Characters.ExecuteDeleteAsync();
      return await db.CampaignSettings.ExecuteUpdateAsync(set => set.SetProperty(s => s.Description, "Changed"));
    });

    await SeedAsync();

    Assert.Equal(full, await RowCountsAsync());
    Assert.Equal("Changed", await Factory.WithDbAsync(db => db.CampaignSettings.Select(s => s.Description).SingleAsync()));
  }

  [Fact]
  public async Task Both_users_sign_in_and_see_role_appropriate_data()
  {
    await SeedAsync();
    var gm = await SignInAsync(DevelopmentSeeder.GmEmail, DevelopmentSeeder.GmPassword);
    var player = await SignInAsync(DevelopmentSeeder.PlayerEmail, DevelopmentSeeder.PlayerPassword);

    var gmSetting = Assert.Single((await GetJsonAsync(gm, "/api/settings")).GetProperty("items").EnumerateArray());
    var playerSetting = Assert.Single((await GetJsonAsync(player, "/api/settings")).GetProperty("items").EnumerateArray());
    Assert.Equal("Osepia", gmSetting.GetProperty("name").GetString());
    Assert.Equal("GameMaster", gmSetting.GetProperty("myRole").GetString());
    Assert.Equal("Player", playerSetting.GetProperty("myRole").GetString());

    var settingId = gmSetting.GetProperty("id").GetGuid();
    var gmEntries = Names(await GetJsonAsync(gm, $"/api/settings/{settingId}/entries?pageSize=100"));
    var playerEntries = Names(await GetJsonAsync(player, $"/api/settings/{settingId}/entries?pageSize=100"));
    Assert.Contains("Cult of Beléna", gmEntries);
    Assert.DoesNotContain("Cult of Beléna", playerEntries);
    Assert.Equal(gmEntries.Count - 1, playerEntries.Count);

    var character = Assert.Single((await GetJsonAsync(player, "/api/characters")).GetProperty("items").EnumerateArray());
    Assert.Equal("Theron Vale", character.GetProperty("name").GetString());
    Assert.Equal("Draft", character.GetProperty("status").GetString());
    Assert.Equal("Sasymon", character.GetProperty("homelandName").GetString());
    Assert.Empty(Names(await GetJsonAsync(gm, "/api/characters")));
  }

  [Fact]
  public async Task Seed_Enabled_seeds_at_startup_in_Development()
  {
    using var seeding = Factory.WithConfig(DevelopmentSeedingSetup.EnabledKey, "true");

    seeding.CreateClient();

    Assert.Equal(2, await Factory.WithDbAsync(db => db.Users.CountAsync()));
  }

  [Fact]
  public async Task Nothing_is_seeded_at_startup_by_default()
  {
    using var plain = Factory.WithWebHostBuilder(_ => { });

    plain.CreateClient();

    Assert.Equal(0, await Factory.WithDbAsync(db => db.Users.CountAsync()));
  }

  [Theory]
  [InlineData("Production")]
  [InlineData("Staging")]
  public async Task Seed_Enabled_stops_startup_outside_Development(string environment)
  {
    using var seeding = Factory
        .WithConfig(DevelopmentSeedingSetup.EnabledKey, "true")
        .WithWebHostBuilder(builder => builder.UseEnvironment(environment));

    var error = Assert.Throws<InvalidOperationException>(() => seeding.CreateClient());

    Assert.Contains("only in Development", error.Message);
    Assert.Equal(0, await Factory.WithDbAsync(db => db.Users.CountAsync()));
  }
}
