using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Sharing;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Tests.Sharing;

/// <summary>P3-02: POST /api/settings/{sid}/invites through HTTP.</summary>
[Collection(PostgresCollection.Name)]
public class InviteCreateTests : PostgresTestBase
{
  public InviteCreateTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static string Url(Guid settingId) => $"/api/settings/{settingId}/invites";

  [Theory]
  [InlineData(SharingWorld.Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(SharingWorld.Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(SharingWorld.Caller.Player, HttpStatusCode.Forbidden)]
  [InlineData(SharingWorld.Caller.GameMaster, HttpStatusCode.Created)]
  [InlineData(SharingWorld.Caller.Owner, HttpStatusCode.Created)]
  public async Task Only_GameMasters_can_create(SharingWorld.Caller caller, HttpStatusCode expected)
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[caller].PostAsJsonAsync(Url(world.SettingId), new { });

    Assert.Equal(expected, response.StatusCode);
    var invites = await Factory.WithDbAsync(db => db.SettingInvites.ToListAsync());
    if (expected == HttpStatusCode.Created)
    {
      var invite = Assert.Single(invites);
      Assert.Equal(world.UserIds[caller], invite.CreatedByUserId);
      Assert.Equal(world.SettingId, invite.CampaignSettingId);
    }
    else
    {
      await JsonAssert.ReadProblemAsync(response);
      Assert.Empty(invites);
    }
  }

  [Fact]
  public async Task Defaults_to_seven_days_and_unlimited_uses()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var before = DateTimeOffset.UtcNow;

    var response = await world[SharingWorld.Caller.Owner].PostAsJsonAsync(Url(world.SettingId), new { });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(response,
        "id", "code", "joinUrl", "status", "expiresAt", "maxUses", "useCount", "revokedAt", "createdAt");
    var code = body.GetProperty("code").GetString()!;
    Assert.Equal(InviteCodes.Length, code.Length);
    Assert.All(code, c => Assert.Contains(c, InviteCodes.Alphabet));
    Assert.Equal($"http://localhost:3000/join/{code}", body.GetProperty("joinUrl").GetString());
    Assert.Equal("Active", body.GetProperty("status").GetString());
    Assert.Equal(0, body.GetProperty("useCount").GetInt32());
    Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("maxUses").ValueKind);
    Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("revokedAt").ValueKind);

    var expiresAt = body.GetProperty("expiresAt").GetDateTimeOffset();
    Assert.InRange(expiresAt, before.AddDays(7), DateTimeOffset.UtcNow.AddDays(7));

    var stored = await Factory.WithDbAsync(db => db.SettingInvites.SingleAsync());
    Assert.Equal(code, stored.Code);
    Assert.Equal(body.GetProperty("id").GetGuid(), stored.Id);
  }

  [Fact]
  public async Task Body_can_be_omitted()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[SharingWorld.Caller.Owner].PostAsync(Url(world.SettingId), null);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.Equal(1, await Factory.WithDbAsync(db => db.SettingInvites.CountAsync()));
  }

  [Fact]
  public async Task Expiry_and_max_uses_can_be_chosen()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var before = DateTimeOffset.UtcNow;

    var response = await world[SharingWorld.Caller.GameMaster].PostAsJsonAsync(
        Url(world.SettingId), new { expiresInDays = 30, maxUses = 5 });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    Assert.Equal(5, body.GetProperty("maxUses").GetInt32());
    Assert.InRange(
        body.GetProperty("expiresAt").GetDateTimeOffset(),
        before.AddDays(30),
        DateTimeOffset.UtcNow.AddDays(30));
  }

  [Theory]
  [InlineData("expiresInDays", 0)]
  [InlineData("expiresInDays", 91)]
  [InlineData("maxUses", 0)]
  [InlineData("maxUses", 101)]
  public async Task Out_of_range_values_are_400(string field, int value)
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[SharingWorld.Caller.Owner].PostAsJsonAsync(
        Url(world.SettingId), new Dictionary<string, int> { [field] = value });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    var key = char.ToUpperInvariant(field[0]) + field[1..];
    Assert.True(problem.GetProperty("errors").TryGetProperty(key, out _),
        $"Expected a validation error keyed by {key}.");
    Assert.Equal(0, await Factory.WithDbAsync(db => db.SettingInvites.CountAsync()));
  }

  [Fact]
  public async Task Twenty_active_invites_is_the_cap()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddInvitesAsync(InviteRules.MaxActivePerSetting);

    var response = await world[SharingWorld.Caller.Owner].PostAsJsonAsync(Url(world.SettingId), new { });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(InviteRules.MaxActivePerSetting,
        await Factory.WithDbAsync(db => db.SettingInvites.CountAsync()));
  }

  [Fact]
  public async Task Revoked_expired_and_exhausted_invites_do_not_count_toward_the_cap()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var now = DateTimeOffset.UtcNow;
    await world.AddInvitesAsync(InviteRules.MaxActivePerSetting - 1);
    await world.AddInvitesAsync(1, i => i.RevokedAt = now);
    await world.AddInvitesAsync(1, i => i.ExpiresAt = now.AddMinutes(-1));
    await world.AddInvitesAsync(1, i => { i.MaxUses = 2; i.UseCount = 2; });

    var response = await world[SharingWorld.Caller.Owner].PostAsJsonAsync(Url(world.SettingId), new { });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Fact]
  public async Task Invites_in_other_settings_do_not_count_toward_the_cap()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var otherId = await world.CreateSettingAsync("Krynn");
    await world.AddInvitesAsync(InviteRules.MaxActivePerSetting, settingId: otherId);

    var response = await world[SharingWorld.Caller.Owner].PostAsJsonAsync(Url(world.SettingId), new { });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Fact]
  public async Task Each_invite_gets_its_own_code()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    for (var i = 0; i < 5; i++)
    {
      var response = await world[SharingWorld.Caller.Owner].PostAsJsonAsync(Url(world.SettingId), new { });
      Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    var codes = await Factory.WithDbAsync(db => db.SettingInvites.Select(i => i.Code).ToListAsync());
    Assert.Equal(5, codes.Distinct().Count());
  }
}
