using System.Net;
using Lorebound.Api.Sharing;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Sharing;

/// <summary>P3-03: listing and revoking invites through HTTP.</summary>
[Collection(PostgresCollection.Name)]
public class InviteManagementTests : PostgresTestBase
{
  public InviteManagementTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static string Url(Guid settingId) => $"/api/settings/{settingId}/invites";

  private Task<DateTimeOffset?> RevokedAtAsync(Guid inviteId) =>
      Factory.WithDbAsync(db => db.SettingInvites
          .Where(i => i.Id == inviteId)
          .Select(i => i.RevokedAt)
          .SingleAsync());

  // ---- list ----

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(Caller.Player, HttpStatusCode.Forbidden)]
  [InlineData(Caller.GameMaster, HttpStatusCode.OK)]
  [InlineData(Caller.Owner, HttpStatusCode.OK)]
  public async Task Only_GameMasters_can_list(Caller caller, HttpStatusCode expected)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddInvitesAsync(1);

    var response = await world[caller].GetAsync(Url(world.SettingId));

    Assert.Equal(expected, response.StatusCode);
    if (expected == HttpStatusCode.OK)
    {
      var body = await JsonAssert.ReadJsonAsync(response);
      Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
    }
    else
    {
      var problem = await JsonAssert.ReadProblemAsync(response);
      Assert.DoesNotContain("code", problem.ToString(), StringComparison.Ordinal);
    }
  }

  [Fact]
  public async Task List_shows_every_status_newest_first()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var now = DateTimeOffset.UtcNow;

    // CreatedAt is the save time, so each later insert is newer.
    // Revoked and also expired and used up: revoked is what the GM sees.
    var revoked = await world.AddInvitesAsync(1, i =>
    {
      i.RevokedAt = now.AddMinutes(-5);
      i.ExpiresAt = now.AddSeconds(-1);
      i.MaxUses = 1;
      i.UseCount = 1;
    });
    var expired = await world.AddInvitesAsync(1, i => i.ExpiresAt = now.AddSeconds(-1));
    var exhausted = await world.AddInvitesAsync(1, i =>
    {
      i.MaxUses = 3;
      i.UseCount = 3;
    });
    var active = await world.AddInvitesAsync(1);

    var response = await world[Caller.GameMaster].GetAsync(Url(world.SettingId));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var items = (await JsonAssert.ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToList();
    Assert.Equal(
        [active[0].Id, exhausted[0].Id, expired[0].Id, revoked[0].Id],
        items.Select(i => i.GetProperty("id").GetGuid()));
    Assert.Equal(
        ["Active", "Exhausted", "Expired", "Revoked"],
        items.Select(i => i.GetProperty("status").GetString()));
    Assert.Equal(
        $"http://localhost:3000/join/{active[0].Code}",
        items[0].GetProperty("joinUrl").GetString());
  }

  [Fact]
  public async Task An_invite_without_expiry_or_use_limit_is_active()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddInvitesAsync(1, i => i.ExpiresAt = null);

    var response = await world[Caller.Owner].GetAsync(Url(world.SettingId));

    var item = (await JsonAssert.ReadJsonAsync(response)).GetProperty("items")[0];
    Assert.Equal("Active", item.GetProperty("status").GetString());
  }

  [Fact]
  public async Task List_only_shows_this_settings_invites()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var otherId = await world.CreateSettingAsync("Krynn");
    var mine = await world.AddInvitesAsync(1);
    await world.AddInvitesAsync(2, settingId: otherId);

    var response = await world[Caller.Owner].GetAsync(Url(world.SettingId));

    var body = await JsonAssert.ReadJsonAsync(response);
    Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
    Assert.Equal(mine[0].Id, body.GetProperty("items")[0].GetProperty("id").GetGuid());
  }

  [Fact]
  public async Task List_is_paged()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddInvitesAsync(5);

    var response = await world[Caller.Owner].GetAsync(Url(world.SettingId) + "?page=2&pageSize=2");

    var body = await JsonAssert.ReadJsonAsync(response);
    Assert.Equal(5, body.GetProperty("totalCount").GetInt32());
    Assert.Equal(2, body.GetProperty("page").GetInt32());
    Assert.Equal(2, body.GetProperty("items").GetArrayLength());
  }

  // ---- revoke ----

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(Caller.Player, HttpStatusCode.Forbidden)]
  [InlineData(Caller.GameMaster, HttpStatusCode.NoContent)]
  [InlineData(Caller.Owner, HttpStatusCode.NoContent)]
  public async Task Only_GameMasters_can_revoke(Caller caller, HttpStatusCode expected)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    var before = DateTimeOffset.UtcNow.AddSeconds(-5);

    var response = await world[caller].DeleteAsync($"{Url(world.SettingId)}/{invite.Id}");

    Assert.Equal(expected, response.StatusCode);
    var revokedAt = await RevokedAtAsync(invite.Id);
    if (expected == HttpStatusCode.NoContent)
    {
      Assert.NotNull(revokedAt);
      Assert.True(revokedAt > before);
    }
    else
    {
      await JsonAssert.ReadProblemAsync(response);
      Assert.Null(revokedAt);
    }
  }

  [Fact]
  public async Task Revoked_invite_is_kept_and_listed_as_revoked()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];

    await world[Caller.Owner].DeleteAsync($"{Url(world.SettingId)}/{invite.Id}");
    var response = await world[Caller.Owner].GetAsync(Url(world.SettingId));

    var item = (await JsonAssert.ReadJsonAsync(response)).GetProperty("items")[0];
    Assert.Equal(invite.Id, item.GetProperty("id").GetGuid());
    Assert.Equal("Revoked", item.GetProperty("status").GetString());
    Assert.NotEqual(System.Text.Json.JsonValueKind.Null, item.GetProperty("revokedAt").ValueKind);
  }

  [Fact]
  public async Task Revoking_twice_is_idempotent_and_keeps_the_first_time()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    var url = $"{Url(world.SettingId)}/{invite.Id}";

    var first = await world[Caller.Owner].DeleteAsync(url);
    var firstRevokedAt = await RevokedAtAsync(invite.Id);
    var second = await world[Caller.GameMaster].DeleteAsync(url);

    Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    Assert.Equal(firstRevokedAt, await RevokedAtAsync(invite.Id));
  }

  [Fact]
  public async Task Revoking_frees_a_slot_under_the_cap()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invites = await world.AddInvitesAsync(InviteRules.MaxActivePerSetting);

    var refused = await world[Caller.Owner].PostAsync(Url(world.SettingId), null);
    await world[Caller.Owner].DeleteAsync($"{Url(world.SettingId)}/{invites[0].Id}");
    var created = await world[Caller.Owner].PostAsync(Url(world.SettingId), null);

    Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
  }

  [Fact]
  public async Task Unknown_invite_is_404()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[Caller.Owner].DeleteAsync($"{Url(world.SettingId)}/{Guid.NewGuid()}");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
  }

  // A GM of one setting cannot reach another setting's invite through their
  // own setting's URL.
  [Fact]
  public async Task Invite_of_another_setting_is_404_and_stays_active()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var otherId = await world.CreateSettingAsync("Krynn");
    var otherInvite = (await world.AddInvitesAsync(1, settingId: otherId))[0];

    var response = await world[Caller.GameMaster].DeleteAsync($"{Url(world.SettingId)}/{otherInvite.Id}");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Null(await RevokedAtAsync(otherInvite.Id));
  }
}
