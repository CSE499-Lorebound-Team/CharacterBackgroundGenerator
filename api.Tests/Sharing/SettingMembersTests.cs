using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Sharing;

/// <summary>P3-06: members list and owner-only role change through HTTP.</summary>
[Collection(PostgresCollection.Name)]
public class SettingMembersTests : PostgresTestBase
{
  public SettingMembersTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static string Url(Guid settingId) => $"/api/settings/{settingId}/members";

  private static string Url(Guid settingId, Guid userId) => $"{Url(settingId)}/{userId}";

  private Task<SettingMembership> MembershipAsync(Guid settingId, Guid userId) =>
      Factory.WithDbAsync(db => db.SettingMemberships
          .SingleAsync(m => m.CampaignSettingId == settingId && m.UserId == userId));

  // ---- list ----

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(Caller.Player, HttpStatusCode.OK)]
  [InlineData(Caller.GameMaster, HttpStatusCode.OK)]
  [InlineData(Caller.Owner, HttpStatusCode.OK)]
  public async Task Any_member_can_list(Caller caller, HttpStatusCode expected)
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[caller].GetAsync(Url(world.SettingId));

    Assert.Equal(expected, response.StatusCode);
    if (expected == HttpStatusCode.OK)
    {
      var body = await JsonAssert.ReadJsonAsync(response);
      Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
    }
    else
    {
      await JsonAssert.ReadProblemAsync(response);
    }
  }

  [Fact]
  public async Task List_is_owner_then_GameMasters_then_Players_without_emails()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[Caller.Player].GetAsync(Url(world.SettingId));

    var raw = await response.Content.ReadAsStringAsync();
    Assert.DoesNotContain("@", raw);
    Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);

    var items = (await JsonAssert.ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToList();
    Assert.All(items, item => Assert.Equal(
        ["displayName", "isOwner", "joinedAt", "role", "userId"],
        item.EnumerateObject().Select(p => p.Name).Order()));

    Assert.Equal(
        [world.UserIds[Caller.Owner], world.UserIds[Caller.GameMaster], world.UserIds[Caller.Player]],
        items.Select(i => i.GetProperty("userId").GetGuid()));
    Assert.Equal(["Owner", "GameMaster", "Player"], items.Select(i => i.GetProperty("displayName").GetString()));
    Assert.Equal(["GameMaster", "GameMaster", "Player"], items.Select(i => i.GetProperty("role").GetString()));
    Assert.Equal([true, false, false], items.Select(i => i.GetProperty("isOwner").GetBoolean()));
  }

  [Fact]
  public async Task Someone_who_accepts_an_invite_is_listed()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    await world[Caller.NonMember].PostAsync($"/api/invites/{invite.Code}/accept", null);

    var body = await JsonAssert.ReadJsonAsync(await world[Caller.Owner].GetAsync(Url(world.SettingId)));

    Assert.Equal(4, body.GetProperty("totalCount").GetInt32());
    var joined = body.GetProperty("items").EnumerateArray()
        .Single(i => i.GetProperty("userId").GetGuid() == world.UserIds[Caller.NonMember]);
    Assert.Equal("Player", joined.GetProperty("role").GetString());
  }

  [Fact]
  public async Task List_only_shows_this_settings_members()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var otherId = await world.CreateSettingAsync("Krynn");

    var body = await JsonAssert.ReadJsonAsync(await world[Caller.Owner].GetAsync(Url(otherId)));

    var only = Assert.Single(body.GetProperty("items").EnumerateArray());
    Assert.Equal(world.UserIds[Caller.Owner], only.GetProperty("userId").GetGuid());
  }

  [Fact]
  public async Task List_is_paged()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var body = await JsonAssert.ReadJsonAsync(
        await world[Caller.Owner].GetAsync(Url(world.SettingId) + "?page=2&pageSize=2"));

    Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
    var only = Assert.Single(body.GetProperty("items").EnumerateArray());
    Assert.Equal(world.UserIds[Caller.Player], only.GetProperty("userId").GetGuid());
  }

  // ---- change role ----

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(Caller.Player, HttpStatusCode.Forbidden)]
  [InlineData(Caller.GameMaster, HttpStatusCode.Forbidden)]
  [InlineData(Caller.Owner, HttpStatusCode.OK)]
  public async Task Only_the_owner_can_change_roles(Caller caller, HttpStatusCode expected)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var playerId = world.UserIds[Caller.Player];

    var response = await world[caller].PatchAsJsonAsync(
        Url(world.SettingId, playerId), new { role = "GameMaster" });

    Assert.Equal(expected, response.StatusCode);
    var stored = await MembershipAsync(world.SettingId, playerId);
    if (expected == HttpStatusCode.OK)
    {
      var body = await JsonAssert.ReadJsonAsync(response);
      Assert.Equal(playerId, body.GetProperty("userId").GetGuid());
      Assert.Equal("GameMaster", body.GetProperty("role").GetString());
      Assert.False(body.GetProperty("isOwner").GetBoolean());
      Assert.Equal(SettingRole.GameMaster, stored.Role);
    }
    else
    {
      await JsonAssert.ReadProblemAsync(response);
      Assert.Equal(SettingRole.Player, stored.Role);
    }
  }

  [Fact]
  public async Task Promoted_player_gains_GameMaster_access()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invitesUrl = $"/api/settings/{world.SettingId}/invites";

    var before = await world[Caller.Player].PostAsync(invitesUrl, null);
    await world[Caller.Owner].PatchAsJsonAsync(
        Url(world.SettingId, world.UserIds[Caller.Player]), new { role = "GameMaster" });
    var after = await world[Caller.Player].PostAsync(invitesUrl, null);

    Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
    Assert.Equal(HttpStatusCode.Created, after.StatusCode);
  }

  [Fact]
  public async Task Owner_can_demote_a_GameMaster_who_then_loses_access()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var gmId = world.UserIds[Caller.GameMaster];

    var response = await world[Caller.Owner].PatchAsJsonAsync(
        Url(world.SettingId, gmId), new { role = "Player" });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(SettingRole.Player, (await MembershipAsync(world.SettingId, gmId)).Role);
    var invites = await world[Caller.GameMaster].GetAsync($"/api/settings/{world.SettingId}/invites");
    Assert.Equal(HttpStatusCode.Forbidden, invites.StatusCode);
  }

  [Fact]
  public async Task Same_role_is_a_no_op_200()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var playerId = world.UserIds[Caller.Player];
    var before = await MembershipAsync(world.SettingId, playerId);

    var response = await world[Caller.Owner].PatchAsJsonAsync(
        Url(world.SettingId, playerId), new { role = "Player" });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("Player", (await JsonAssert.ReadJsonAsync(response)).GetProperty("role").GetString());
    var after = await MembershipAsync(world.SettingId, playerId);
    Assert.Equal(before.UpdatedAt, after.UpdatedAt);
  }

  [Fact]
  public async Task Owner_cannot_be_demoted()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var ownerId = world.UserIds[Caller.Owner];

    var response = await world[Caller.Owner].PatchAsJsonAsync(
        Url(world.SettingId, ownerId), new { role = "Player" });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(SettingRole.GameMaster, (await MembershipAsync(world.SettingId, ownerId)).Role);
  }

  [Fact]
  public async Task Owner_setting_themself_to_GameMaster_is_a_no_op_200()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[Caller.Owner].PatchAsJsonAsync(
        Url(world.SettingId, world.UserIds[Caller.Owner]), new { role = "GameMaster" });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True((await JsonAssert.ReadJsonAsync(response)).GetProperty("isOwner").GetBoolean());
  }

  [Fact]
  public async Task Non_member_or_unknown_user_is_404()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var nonMember = await world[Caller.Owner].PatchAsJsonAsync(
        Url(world.SettingId, world.UserIds[Caller.NonMember]), new { role = "GameMaster" });
    var unknown = await world[Caller.Owner].PatchAsJsonAsync(
        Url(world.SettingId, Guid.NewGuid()), new { role = "GameMaster" });

    Assert.Equal(HttpStatusCode.NotFound, nonMember.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    Assert.False(await Factory.WithDbAsync(db => db.SettingMemberships
        .AnyAsync(m => m.UserId == world.UserIds[Caller.NonMember])));
  }

  public static TheoryData<object> InvalidBodies => new()
  {
    new { role = "Owner" },
    new { role = "" },
    new { role = 7 },
    new { },
  };

  [Theory]
  [MemberData(nameof(InvalidBodies))]
  public async Task Invalid_role_is_400(object body)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var playerId = world.UserIds[Caller.Player];

    var response = await world[Caller.Owner].PatchAsJsonAsync(Url(world.SettingId, playerId), body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(SettingRole.Player, (await MembershipAsync(world.SettingId, playerId)).Role);
  }
}
