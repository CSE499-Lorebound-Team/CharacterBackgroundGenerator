using System.Net;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Sharing;

/// <summary>P3-07: DELETE /api/settings/{sid}/members/{userId} (remove and leave).</summary>
[Collection(PostgresCollection.Name)]
public class MemberRemovalTests : PostgresTestBase
{
  public MemberRemovalTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static string Url(Guid settingId, Guid userId) => $"/api/settings/{settingId}/members/{userId}";

  private Task<bool> IsMemberAsync(Guid settingId, Guid userId) =>
      Factory.WithDbAsync(db => db.SettingMemberships
          .AnyAsync(m => m.CampaignSettingId == settingId && m.UserId == userId));

  /// <summary>Who removes whom, using the world's owner, GameMaster and Player.</summary>
  [Theory]
  [InlineData(Caller.Anonymous, Caller.Player, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, Caller.Player, HttpStatusCode.NotFound)]
  [InlineData(Caller.Player, Caller.Player, HttpStatusCode.NoContent)]
  [InlineData(Caller.Player, Caller.GameMaster, HttpStatusCode.Forbidden)]
  [InlineData(Caller.Player, Caller.Owner, HttpStatusCode.Forbidden)]
  [InlineData(Caller.GameMaster, Caller.GameMaster, HttpStatusCode.NoContent)]
  [InlineData(Caller.GameMaster, Caller.Player, HttpStatusCode.NoContent)]
  [InlineData(Caller.GameMaster, Caller.Owner, HttpStatusCode.Forbidden)]
  [InlineData(Caller.Owner, Caller.Owner, HttpStatusCode.Conflict)]
  [InlineData(Caller.Owner, Caller.GameMaster, HttpStatusCode.NoContent)]
  [InlineData(Caller.Owner, Caller.Player, HttpStatusCode.NoContent)]
  public async Task Permission_matrix(Caller caller, Caller target, HttpStatusCode expected)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var targetId = world.UserIds[target];

    var response = await world[caller].DeleteAsync(Url(world.SettingId, targetId));

    Assert.Equal(expected, response.StatusCode);
    Assert.Equal(expected != HttpStatusCode.NoContent, await IsMemberAsync(world.SettingId, targetId));
    if (expected != HttpStatusCode.NoContent)
    {
      await JsonAssert.ReadProblemAsync(response);
    }
  }

  [Fact]
  public async Task Player_cannot_remove_another_Player()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var (_, otherPlayerId) = await world.AddMemberAsync("Other Player", SettingRole.Player);

    var response = await world[Caller.Player].DeleteAsync(Url(world.SettingId, otherPlayerId));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.True(await IsMemberAsync(world.SettingId, otherPlayerId));
  }

  [Fact]
  public async Task Non_owner_GameMaster_cannot_remove_another_GameMaster()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var (_, otherGmId) = await world.AddMemberAsync("Other GM", SettingRole.GameMaster);

    var response = await world[Caller.GameMaster].DeleteAsync(Url(world.SettingId, otherGmId));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
    Assert.True(await IsMemberAsync(world.SettingId, otherGmId));
  }

  [Fact]
  public async Task Owner_cannot_leave_and_is_told_to_delete_the_setting()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[Caller.Owner].DeleteAsync(Url(world.SettingId, world.UserIds[Caller.Owner]));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.Contains("Delete the setting", problem.GetProperty("detail").GetString());
  }

  [Fact]
  public async Task Removing_someone_who_is_not_a_member_is_404()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var nonMember = await world[Caller.GameMaster].DeleteAsync(Url(world.SettingId, world.UserIds[Caller.NonMember]));
    var unknown = await world[Caller.Owner].DeleteAsync(Url(world.SettingId, Guid.NewGuid()));

    Assert.Equal(HttpStatusCode.NotFound, nonMember.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
  }

  // A Player is refused before the target is looked up, so they cannot use
  // 403 vs 404 to find out who belongs.
  [Fact]
  public async Task Player_gets_403_whether_or_not_the_target_belongs()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var member = await world[Caller.Player].DeleteAsync(Url(world.SettingId, world.UserIds[Caller.GameMaster]));
    var stranger = await world[Caller.Player].DeleteAsync(Url(world.SettingId, world.UserIds[Caller.NonMember]));

    Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
  }

  [Fact]
  public async Task Removing_twice_is_404_the_second_time()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var url = Url(world.SettingId, world.UserIds[Caller.Player]);

    var first = await world[Caller.Owner].DeleteAsync(url);
    var second = await world[Caller.Owner].DeleteAsync(url);

    Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
  }

  /// <summary>Acceptance criterion: a removed user immediately gets 404 on the setting.</summary>
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Removed_or_departed_user_immediately_loses_access(bool leftThemself)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var player = world[Caller.Player];
    Assert.Equal(HttpStatusCode.OK, (await player.GetAsync($"/api/settings/{world.SettingId}")).StatusCode);

    var remover = leftThemself ? player : world[Caller.GameMaster];
    var response = await remover.DeleteAsync(Url(world.SettingId, world.UserIds[Caller.Player]));

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync($"/api/settings/{world.SettingId}")).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync($"/api/settings/{world.SettingId}/members")).StatusCode);
    var list = await JsonAssert.ReadJsonAsync(await player.GetAsync("/api/settings"));
    Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
  }

  [Fact]
  public async Task Removed_GameMaster_loses_invite_access_but_their_invites_and_account_remain()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var gm = world[Caller.GameMaster];
    var created = await gm.PostAsync($"/api/settings/{world.SettingId}/invites", null);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);

    await world[Caller.Owner].DeleteAsync(Url(world.SettingId, world.UserIds[Caller.GameMaster]));

    Assert.Equal(HttpStatusCode.NotFound, (await gm.PostAsync($"/api/settings/{world.SettingId}/invites", null)).StatusCode);
    Assert.Equal(1, await Factory.WithDbAsync(db => db.SettingInvites.CountAsync()));
    Assert.True(await Factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == world.UserIds[Caller.GameMaster])));
  }

  [Fact]
  public async Task Removing_a_member_leaves_everyone_else_in_place()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    await world[Caller.Owner].DeleteAsync(Url(world.SettingId, world.UserIds[Caller.Player]));

    var remaining = await Factory.WithDbAsync(db => db.SettingMemberships
        .Where(m => m.CampaignSettingId == world.SettingId)
        .Select(m => m.UserId)
        .ToListAsync());
    Assert.Equal(
        new[] { world.UserIds[Caller.Owner], world.UserIds[Caller.GameMaster] }.Order(),
        remaining.Order());
  }

  [Fact]
  public async Task Someone_who_left_can_rejoin_with_an_invite()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    await world[Caller.Player].DeleteAsync(Url(world.SettingId, world.UserIds[Caller.Player]));

    var rejoin = await world[Caller.Player].PostAsync($"/api/invites/{invite.Code}/accept", null);

    Assert.Equal(HttpStatusCode.OK, rejoin.StatusCode);
    Assert.True(await IsMemberAsync(world.SettingId, world.UserIds[Caller.Player]));
    Assert.Equal(1, await Factory.WithDbAsync(db => db.SettingInvites.Select(i => i.UseCount).SingleAsync()));
  }
}
