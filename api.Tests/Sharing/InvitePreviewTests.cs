using System.Net;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Sharing;

/// <summary>P3-04: GET /api/invites/{code} through HTTP.</summary>
[Collection(PostgresCollection.Name)]
public class InvitePreviewTests : PostgresTestBase
{
  public InvitePreviewTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static string Url(string code) => $"/api/invites/{Uri.EscapeDataString(code)}";

  // Everything but traceId, which differs per request by design.
  private static string Comparable(JsonElement problem) =>
      JsonSerializer.Serialize(problem.EnumerateObject()
          .Where(p => p.Name != "traceId")
          .ToDictionary(p => p.Name, p => p.Value.ToString()));

  [Fact]
  public async Task Anonymous_is_401()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];

    var response = await world[Caller.Anonymous].GetAsync(Url(invite.Code));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
  }

  [Theory]
  [InlineData(Caller.NonMember, false)]
  [InlineData(Caller.Player, true)]
  [InlineData(Caller.GameMaster, true)]
  [InlineData(Caller.Owner, true)]
  public async Task Valid_code_shows_the_setting(Caller caller, bool alreadyMember)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];

    var response = await world[caller].GetAsync(Url(invite.Code));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(
        response, "settingName", "gmDisplayName", "alreadyMember");
    Assert.Equal("Osepia", body.GetProperty("settingName").GetString());
    Assert.Equal("Owner", body.GetProperty("gmDisplayName").GetString());
    Assert.Equal(alreadyMember, body.GetProperty("alreadyMember").GetBoolean());
  }

  [Fact]
  public async Task Owner_without_a_membership_row_counts_as_a_member()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    await Factory.WithDbAsync(db => db.SettingMemberships
        .Where(m => m.UserId == world.UserIds[Caller.Owner])
        .ExecuteDeleteAsync());

    var body = await JsonAssert.ReadJsonAsync(await world[Caller.Owner].GetAsync(Url(invite.Code)));

    Assert.True(body.GetProperty("alreadyMember").GetBoolean());
  }

  [Theory]
  [InlineData("lower")]
  [InlineData("hyphen")]
  public async Task Code_is_read_leniently(string variant)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    var typed = variant == "lower"
        ? invite.Code.ToLowerInvariant()
        : $"{invite.Code[..5]}-{invite.Code[5..]}";

    var response = await world[Caller.NonMember].GetAsync(Url(typed));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Invite_without_expiry_or_use_limit_is_valid()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1, i => i.ExpiresAt = null))[0];

    var response = await world[Caller.NonMember].GetAsync(Url(invite.Code));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Invite_with_uses_left_is_valid()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1, i => { i.MaxUses = 3; i.UseCount = 2; }))[0];

    var response = await world[Caller.NonMember].GetAsync(Url(invite.Code));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Previewing_does_not_use_up_the_invite()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1, i => i.MaxUses = 1))[0];

    for (var i = 0; i < 3; i++)
    {
      Assert.Equal(HttpStatusCode.OK, (await world[Caller.NonMember].GetAsync(Url(invite.Code))).StatusCode);
    }

    var stored = await Factory.WithDbAsync(db => db.SettingInvites.SingleAsync());
    Assert.Equal(0, stored.UseCount);
    Assert.False(await Factory.WithDbAsync(db =>
        db.SettingMemberships.AnyAsync(m => m.UserId == world.UserIds[Caller.NonMember])));
  }

  /// <summary>
  /// Unknown, malformed, expired, revoked and used-up codes must be
  /// indistinguishable, so a guesser learns nothing about which codes exist.
  /// </summary>
  [Fact]
  public async Task Every_invalid_code_gets_the_identical_404()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var now = DateTimeOffset.UtcNow;
    var expired = (await world.AddInvitesAsync(1, i => i.ExpiresAt = now.AddSeconds(-1)))[0];
    var revoked = (await world.AddInvitesAsync(1, i => i.RevokedAt = now))[0];
    var exhausted = (await world.AddInvitesAsync(1, i => { i.MaxUses = 2; i.UseCount = 2; }))[0];

    var codes = new Dictionary<string, string>
    {
      ["unknown"] = "ZZZZZZZZZZ",
      ["malformed"] = "not-a-code!",
      ["too short"] = "ABC",
      ["expired"] = expired.Code,
      ["revoked"] = revoked.Code,
      ["exhausted"] = exhausted.Code,
    };

    var bodies = new Dictionary<string, string>();
    foreach (var (label, code) in codes)
    {
      var response = await world[Caller.NonMember].GetAsync(Url(code));
      Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{label}: {response.StatusCode}");
      bodies[label] = Comparable(await JsonAssert.ReadProblemAsync(response));
    }

    Assert.Single(bodies.Values.Distinct());
    Assert.Contains("Invite not found.", bodies["unknown"]);
  }

  // A member of the setting gets no special treatment for a dead code.
  [Fact]
  public async Task Members_also_get_404_for_a_revoked_code()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var revoked = (await world.AddInvitesAsync(1, i => i.RevokedAt = DateTimeOffset.UtcNow))[0];

    var response = await world[Caller.Owner].GetAsync(Url(revoked.Code));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }
}
