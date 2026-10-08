using System.Net;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Sharing;

/// <summary>P3-05: POST /api/invites/{code}/accept through HTTP.</summary>
[Collection(PostgresCollection.Name)]
public class InviteAcceptTests : PostgresTestBase
{
  public InviteAcceptTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static string Url(string code) => $"/api/invites/{Uri.EscapeDataString(code)}/accept";

  private Task<int> UseCountAsync(Guid inviteId) =>
      Factory.WithDbAsync(db => db.SettingInvites
          .Where(i => i.Id == inviteId)
          .Select(i => i.UseCount)
          .SingleAsync());

  private Task<int> MembershipCountAsync() =>
      Factory.WithDbAsync(db => db.SettingMemberships.CountAsync());

  // Everything but traceId, which differs per request by design.
  private static string Comparable(JsonElement problem) =>
      JsonSerializer.Serialize(problem.EnumerateObject()
          .Where(p => p.Name != "traceId")
          .ToDictionary(p => p.Name, p => p.Value.ToString()));

  [Fact]
  public async Task Anonymous_is_401_and_uses_nothing()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];

    var response = await world[Caller.Anonymous].PostAsync(Url(invite.Code), null);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Equal(0, await UseCountAsync(invite.Id));
  }

  [Fact]
  public async Task Non_member_joins_as_Player_and_uses_one_use()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1, i => i.MaxUses = 5))[0];
    var before = DateTimeOffset.UtcNow.AddSeconds(-5);

    var response = await world[Caller.NonMember].PostAsync(Url(invite.Code), null);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(response, "settingId", "myRole");
    Assert.Equal(world.SettingId, body.GetProperty("settingId").GetGuid());
    Assert.Equal("Player", body.GetProperty("myRole").GetString());

    var membership = await Factory.WithDbAsync(db => db.SettingMemberships
        .SingleAsync(m => m.UserId == world.UserIds[Caller.NonMember]));
    Assert.Equal((world.SettingId, SettingRole.Player), (membership.CampaignSettingId, membership.Role));
    Assert.True(membership.JoinedAt > before);
    Assert.Equal(1, await UseCountAsync(invite.Id));

    // The new member can now open the setting.
    var detail = await world[Caller.NonMember].GetAsync($"/api/settings/{world.SettingId}");
    Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    Assert.Equal("Player", (await JsonAssert.ReadJsonAsync(detail)).GetProperty("myRole").GetString());
  }

  [Theory]
  [InlineData(Caller.Player, "Player")]
  [InlineData(Caller.GameMaster, "GameMaster")]
  [InlineData(Caller.Owner, "GameMaster")]
  public async Task Existing_member_gets_200_with_their_role_and_uses_nothing(Caller caller, string role)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1, i => i.MaxUses = 1))[0];
    var membershipsBefore = await MembershipCountAsync();

    var response = await world[caller].PostAsync(Url(invite.Code), null);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    Assert.Equal(world.SettingId, body.GetProperty("settingId").GetGuid());
    Assert.Equal(role, body.GetProperty("myRole").GetString());
    Assert.Equal(0, await UseCountAsync(invite.Id));
    Assert.Equal(membershipsBefore, await MembershipCountAsync());
  }

  [Fact]
  public async Task Accepting_again_is_idempotent()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];

    var first = await world[Caller.NonMember].PostAsync(Url(invite.Code), null);
    var second = await world[Caller.NonMember].PostAsync(Url(invite.Code), null);

    Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    Assert.Equal("Player", (await JsonAssert.ReadJsonAsync(second)).GetProperty("myRole").GetString());
    Assert.Equal(1, await UseCountAsync(invite.Id));
    Assert.Equal(1, await Factory.WithDbAsync(db => db.SettingMemberships
        .CountAsync(m => m.UserId == world.UserIds[Caller.NonMember])));
  }

  // Found by the smoke run: the retry of an accept that used the last use
  // (double click, reload) must not turn into a 404.
  [Fact]
  public async Task Accepting_again_after_using_the_last_use_is_still_200()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1, i => i.MaxUses = 1))[0];

    var first = await world[Caller.NonMember].PostAsync(Url(invite.Code), null);
    var retry = await world[Caller.NonMember].PostAsync(Url(invite.Code), null);

    Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(retry);
    Assert.Equal(world.SettingId, body.GetProperty("settingId").GetGuid());
    Assert.Equal("Player", body.GetProperty("myRole").GetString());
    Assert.Equal(1, await UseCountAsync(invite.Id));
  }

  /// <summary>
  /// Membership is checked before validity: a member of the code's setting
  /// gets 200 whatever state the code is in, and nothing changes. They
  /// already belong, so this tells them nothing new.
  /// </summary>
  [Theory]
  [InlineData("expired")]
  [InlineData("revoked")]
  [InlineData("exhausted")]
  public async Task Member_gets_200_even_for_an_unusable_code_of_their_setting(string state)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var now = DateTimeOffset.UtcNow;
    var invite = (await world.AddInvitesAsync(1, i =>
    {
      switch (state)
      {
        case "expired": i.ExpiresAt = now.AddSeconds(-1); break;
        case "revoked": i.RevokedAt = now; break;
        default: i.MaxUses = 1; i.UseCount = 1; break;
      }
    }))[0];
    var useCount = invite.UseCount;

    var response = await world[Caller.Player].PostAsync(Url(invite.Code), null);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("Player", (await JsonAssert.ReadJsonAsync(response)).GetProperty("myRole").GetString());
    Assert.Equal(useCount, await UseCountAsync(invite.Id));
  }

  // The relaxation is only for the code's own setting: a member of another
  // setting is a non-member here and still gets the identical 404.
  [Fact]
  public async Task Member_of_another_setting_still_gets_404_for_an_unusable_code()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");
    var revoked = (await world.AddInvitesAsync(1, i => i.RevokedAt = DateTimeOffset.UtcNow, krynnId))[0];

    var response = await world[Caller.Player].PostAsync(Url(revoked.Code), null);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.False(await Factory.WithDbAsync(db => db.SettingMemberships
        .AnyAsync(m => m.CampaignSettingId == krynnId && m.UserId == world.UserIds[Caller.Player])));
  }

  [Fact]
  public async Task Code_is_read_leniently()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    var typed = $"{invite.Code[..5]}-{invite.Code[5..]}".ToLowerInvariant();

    var response = await world[Caller.NonMember].PostAsync(Url(typed), null);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Last_use_lets_one_user_in_and_then_the_code_is_exhausted()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1, i => i.MaxUses = 1))[0];
    var (latecomer, _) = await Factory.CreateSignedInClientAsync("late@example.com", "Late");

    var first = await world[Caller.NonMember].PostAsync(Url(invite.Code), null);
    var second = await latecomer.PostAsync(Url(invite.Code), null);

    Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    Assert.Equal(1, await UseCountAsync(invite.Id));

    var list = await world[Caller.Owner].GetAsync($"/api/settings/{world.SettingId}/invites");
    var item = (await JsonAssert.ReadJsonAsync(list)).GetProperty("items")[0];
    Assert.Equal("Exhausted", item.GetProperty("status").GetString());
  }

  // P3-03 acceptance: a revoked code can no longer be accepted.
  [Fact]
  public async Task Revoked_code_cannot_be_accepted()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];

    var revoke = await world[Caller.Owner].DeleteAsync($"/api/settings/{world.SettingId}/invites/{invite.Id}");
    var response = await world[Caller.NonMember].PostAsync(Url(invite.Code), null);

    Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.False(await Factory.WithDbAsync(db => db.SettingMemberships
        .AnyAsync(m => m.UserId == world.UserIds[Caller.NonMember])));
  }

  /// <summary>
  /// Same rule as preview: every unusable code is the identical 404, the same
  /// body preview returns, and nothing changes.
  /// </summary>
  [Fact]
  public async Task Every_invalid_code_gets_the_identical_404_and_changes_nothing()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var now = DateTimeOffset.UtcNow;
    var expired = (await world.AddInvitesAsync(1, i => i.ExpiresAt = now.AddSeconds(-1)))[0];
    var revoked = (await world.AddInvitesAsync(1, i => i.RevokedAt = now))[0];
    var exhausted = (await world.AddInvitesAsync(1, i => { i.MaxUses = 2; i.UseCount = 2; }))[0];
    var membershipsBefore = await MembershipCountAsync();

    var codes = new Dictionary<string, string>
    {
      ["unknown"] = "ZZZZZZZZZZ",
      ["malformed"] = "not-a-code!",
      ["expired"] = expired.Code,
      ["revoked"] = revoked.Code,
      ["exhausted"] = exhausted.Code,
    };

    var bodies = new List<string>();
    foreach (var (label, code) in codes)
    {
      var response = await world[Caller.NonMember].PostAsync(Url(code), null);
      Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{label}: {response.StatusCode}");
      bodies.Add(Comparable(await JsonAssert.ReadProblemAsync(response)));
    }

    var preview = await world[Caller.NonMember].GetAsync("/api/invites/ZZZZZZZZZZ");
    bodies.Add(Comparable(await JsonAssert.ReadProblemAsync(preview)));

    Assert.Single(bodies.Distinct());
    Assert.Equal(membershipsBefore, await MembershipCountAsync());
    Assert.Equal(2, await UseCountAsync(exhausted.Id));
    Assert.Equal(0, await UseCountAsync(expired.Id));
    Assert.Equal(0, await UseCountAsync(revoked.Id));
  }

  // ---- races ----

  /// <summary>
  /// Acceptance criterion: parallel accepts of a 1-use code by different
  /// users end with exactly one new membership. Repeated, because a race
  /// that only sometimes overlaps proves little once.
  /// </summary>
  [Fact]
  public async Task Parallel_accepts_of_a_one_use_code_admit_exactly_one_user()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var clients = new List<HttpClient>();
    for (var i = 0; i < 6; i++)
    {
      clients.Add((await Factory.CreateSignedInClientAsync($"racer{i}@example.com", $"Racer {i}")).Client);
    }

    for (var round = 0; round < 5; round++)
    {
      var invite = (await world.AddInvitesAsync(1, i => i.MaxUses = 1))[0];
      await Factory.WithDbAsync(db => db.SettingMemberships
          .Where(m => m.CampaignSettingId == world.SettingId && m.Role == SettingRole.Player
              && m.UserId != world.UserIds[Caller.Player])
          .ExecuteDeleteAsync());

      var responses = await Task.WhenAll(clients.Select(c => c.PostAsync(Url(invite.Code), null)));

      Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
      Assert.Equal(clients.Count - 1, responses.Count(r => r.StatusCode == HttpStatusCode.NotFound));
      Assert.Equal(1, await UseCountAsync(invite.Id));
      Assert.Equal(1, await Factory.WithDbAsync(db => db.SettingMemberships
          .CountAsync(m => m.CampaignSettingId == world.SettingId && m.Role == SettingRole.Player
              && m.UserId != world.UserIds[Caller.Player])));
    }
  }

  [Fact]
  public async Task Parallel_accepts_by_one_user_join_once_and_use_one_use()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1, i => i.MaxUses = 10))[0];
    var client = world[Caller.NonMember];

    var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsync(Url(invite.Code), null)));

    Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    foreach (var response in responses)
    {
      Assert.Equal("Player", (await JsonAssert.ReadJsonAsync(response)).GetProperty("myRole").GetString());
    }
    Assert.Equal(1, await UseCountAsync(invite.Id));
    Assert.Equal(1, await Factory.WithDbAsync(db => db.SettingMemberships
        .CountAsync(m => m.UserId == world.UserIds[Caller.NonMember])));
  }
}
