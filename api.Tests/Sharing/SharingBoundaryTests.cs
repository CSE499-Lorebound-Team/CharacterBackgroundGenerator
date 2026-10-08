using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Sharing;

/// <summary>
/// P3-08: checks that span every sharing endpoint at once, on top of the
/// per-endpoint suites (InviteCreate, InviteManagement, InvitePreview,
/// InviteAccept, SettingMembers, MemberRemoval). Invites are a security
/// boundary, so each rule here is asserted across the whole surface.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SharingBoundaryTests : PostgresTestBase
{
  public SharingBoundaryTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  /// <summary>Every endpoint scoped to a setting, as (method, path) for that setting.</summary>
  private static IEnumerable<(HttpMethod Method, string Path)> SettingScopedEndpoints(
      Guid settingId, Guid inviteId, Guid userId) =>
  [
    (HttpMethod.Post, $"/api/settings/{settingId}/invites"),
    (HttpMethod.Get, $"/api/settings/{settingId}/invites"),
    (HttpMethod.Delete, $"/api/settings/{settingId}/invites/{inviteId}"),
    (HttpMethod.Get, $"/api/settings/{settingId}/members"),
    (HttpMethod.Patch, $"/api/settings/{settingId}/members/{userId}"),
    (HttpMethod.Delete, $"/api/settings/{settingId}/members/{userId}"),
  ];

  private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path)
  {
    var request = new HttpRequestMessage(method, path);
    if (method == HttpMethod.Patch)
    {
      request.Content = JsonContent.Create(new { role = "GameMaster" });
    }

    return client.SendAsync(request);
  }

  // Everything but traceId, which differs per request by design.
  private static string Comparable(JsonElement problem) =>
      JsonSerializer.Serialize(problem.EnumerateObject()
          .Where(p => p.Name != "traceId")
          .ToDictionary(p => p.Name, p => p.Value.ToString()));

  [Fact]
  public async Task Anonymous_gets_401_from_every_sharing_endpoint_and_nothing_changes()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    var anonymous = world[Caller.Anonymous];

    var endpoints = SettingScopedEndpoints(world.SettingId, invite.Id, world.UserIds[Caller.Player])
        .Append((HttpMethod.Get, $"/api/invites/{invite.Code}"))
        .Append((HttpMethod.Post, $"/api/invites/{invite.Code}/accept"));

    foreach (var (method, path) in endpoints)
    {
      var response = await SendAsync(anonymous, method, path);
      Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{method} {path}: {response.StatusCode}");
      await JsonAssert.ReadProblemAsync(response);
    }

    var stored = await Factory.WithDbAsync(db => db.SettingInvites.SingleAsync());
    Assert.Null(stored.RevokedAt);
    Assert.Equal(0, stored.UseCount);
    Assert.Equal(3, await Factory.WithDbAsync(db => db.SettingMemberships.CountAsync()));
  }

  /// <summary>
  /// A non-member must not be able to tell a real setting from a missing one
  /// through any sharing endpoint, as with the settings endpoints themselves.
  /// </summary>
  [Fact]
  public async Task Non_member_gets_the_same_404_for_a_real_and_a_missing_setting_everywhere()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    var nonMember = world[Caller.NonMember];
    var playerId = world.UserIds[Caller.Player];

    var real = SettingScopedEndpoints(world.SettingId, invite.Id, playerId).ToList();
    var missing = SettingScopedEndpoints(Guid.NewGuid(), invite.Id, playerId).ToList();

    for (var i = 0; i < real.Count; i++)
    {
      var realResponse = await SendAsync(nonMember, real[i].Method, real[i].Path);
      var missingResponse = await SendAsync(nonMember, missing[i].Method, missing[i].Path);

      var label = $"{real[i].Method} {real[i].Path}";
      Assert.True(realResponse.StatusCode == HttpStatusCode.NotFound, $"{label}: {realResponse.StatusCode}");
      Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
      Assert.Equal(
          Comparable(await JsonAssert.ReadProblemAsync(missingResponse)),
          Comparable(await JsonAssert.ReadProblemAsync(realResponse)));
    }

    Assert.Null((await Factory.WithDbAsync(db => db.SettingInvites.SingleAsync())).RevokedAt);
    Assert.Equal(1, await Factory.WithDbAsync(db => db.SettingInvites.CountAsync()));
  }

  [Fact]
  public async Task An_invite_joins_its_own_setting_and_no_other()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");
    var krynnInvite = (await world.AddInvitesAsync(1, settingId: krynnId))[0];

    var response = await world[Caller.NonMember].PostAsync($"/api/invites/{krynnInvite.Code}/accept", null);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(krynnId, (await JsonAssert.ReadJsonAsync(response)).GetProperty("settingId").GetGuid());
    var joined = await Factory.WithDbAsync(db => db.SettingMemberships
        .Where(m => m.UserId == world.UserIds[Caller.NonMember])
        .Select(m => m.CampaignSettingId)
        .ToListAsync());
    Assert.Equal([krynnId], joined);
    Assert.Equal(HttpStatusCode.NotFound,
        (await world[Caller.NonMember].GetAsync($"/api/settings/{world.SettingId}")).StatusCode);
  }

  // A GameMaster of one setting cannot reach members of another setting by
  // putting their userId under their own setting's URL.
  [Fact]
  public async Task Members_of_another_setting_cannot_be_changed_or_removed_through_this_one()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var krynnId = await world.CreateSettingAsync("Krynn");
    var krynnInvite = (await world.AddInvitesAsync(1, settingId: krynnId))[0];
    await world[Caller.NonMember].PostAsync($"/api/invites/{krynnInvite.Code}/accept", null);
    var outsiderId = world.UserIds[Caller.NonMember];

    var patch = await world[Caller.Owner].PatchAsJsonAsync(
        $"/api/settings/{world.SettingId}/members/{outsiderId}", new { role = "GameMaster" });
    var delete = await world[Caller.GameMaster].DeleteAsync(
        $"/api/settings/{world.SettingId}/members/{outsiderId}");

    Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    var membership = await Factory.WithDbAsync(db => db.SettingMemberships
        .SingleAsync(m => m.UserId == outsiderId));
    Assert.Equal((krynnId, Lorebound.Api.Models.SettingRole.Player), (membership.CampaignSettingId, membership.Role));
  }

  [Fact]
  public async Task Codes_of_a_deleted_setting_stop_working()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];

    var deleted = await world[Caller.Owner].DeleteAsync($"/api/settings/{world.SettingId}");
    var preview = await world[Caller.NonMember].GetAsync($"/api/invites/{invite.Code}");
    var accept = await world[Caller.NonMember].PostAsync($"/api/invites/{invite.Code}/accept", null);

    Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
    Assert.Equal(0, await Factory.WithDbAsync(db => db.SettingInvites.CountAsync()));
    Assert.Equal(0, await Factory.WithDbAsync(db => db.SettingMemberships.CountAsync()));
  }

  /// <summary>
  /// No sharing response, for any caller, carries an email address. Members
  /// are shown by display name; invites by code.
  /// </summary>
  [Fact]
  public async Task No_sharing_response_contains_an_email()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    var bodies = new List<(string Label, string Body)>();

    async Task Record(string label, Task<HttpResponseMessage> call)
    {
      var response = await call;
      Assert.True(response.IsSuccessStatusCode, $"{label}: {response.StatusCode}");
      bodies.Add((label, await response.Content.ReadAsStringAsync()));
    }

    await Record("create invite", world[Caller.GameMaster].PostAsync($"/api/settings/{world.SettingId}/invites", null));
    await Record("list invites", world[Caller.Owner].GetAsync($"/api/settings/{world.SettingId}/invites"));
    await Record("preview", world[Caller.NonMember].GetAsync($"/api/invites/{invite.Code}"));
    await Record("accept", world[Caller.NonMember].PostAsync($"/api/invites/{invite.Code}/accept", null));
    foreach (var caller in new[] { Caller.Player, Caller.GameMaster, Caller.Owner, Caller.NonMember })
    {
      await Record($"members as {caller}", world[caller].GetAsync($"/api/settings/{world.SettingId}/members"));
    }
    await Record("change role", world[Caller.Owner].PatchAsJsonAsync(
        $"/api/settings/{world.SettingId}/members/{world.UserIds[Caller.Player]}", new { role = "GameMaster" }));

    Assert.All(bodies, b =>
    {
      Assert.False(b.Body.Contains('@'), $"{b.Label} contains '@': {b.Body}");
      Assert.False(b.Body.Contains("email", StringComparison.OrdinalIgnoreCase), $"{b.Label} mentions email");
    });
  }
}
