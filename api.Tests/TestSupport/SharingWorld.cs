using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Sharing;

namespace Lorebound.Api.Tests.TestSupport;

/// <summary>
/// One setting created by the owner through the API, plus a GameMaster (not
/// the owner), a Player, a signed-in non-member and an anonymous client, for
/// the sharing tests (Phase 3).
/// </summary>
public sealed class SharingWorld
{
  public enum Caller
  {
    Anonymous,
    NonMember,
    Player,
    GameMaster,
    Owner,
  }

  private readonly CustomWebApplicationFactory _factory;

  private SharingWorld(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  public Guid SettingId { get; private set; }

  public Dictionary<Caller, HttpClient> Clients { get; } = new();

  /// <summary>User ids for every caller except <see cref="Caller.Anonymous"/>.</summary>
  public Dictionary<Caller, Guid> UserIds { get; } = new();

  public HttpClient this[Caller caller] => Clients[caller];

  public static async Task<SharingWorld> SeedAsync(CustomWebApplicationFactory factory)
  {
    var world = new SharingWorld(factory);
    world.Clients[Caller.Anonymous] = factory.CreateCookieClient();

    foreach (var caller in new[] { Caller.Owner, Caller.GameMaster, Caller.Player, Caller.NonMember })
    {
      var (client, user) = await factory.CreateSignedInClientAsync(
          $"{caller}@example.com".ToLowerInvariant(), caller.ToString());
      world.Clients[caller] = client;
      world.UserIds[caller] = user.Id;
    }

    world.SettingId = await world.CreateSettingAsync("Osepia");

    await factory.WithDbAsync(db =>
    {
      db.SettingMemberships.AddRange(
          Membership(world.SettingId, world.UserIds[Caller.GameMaster], SettingRole.GameMaster),
          Membership(world.SettingId, world.UserIds[Caller.Player], SettingRole.Player));
      return db.SaveChangesAsync();
    });

    return world;
  }

  /// <summary>Another setting owned by <see cref="Caller.Owner"/> alone.</summary>
  public async Task<Guid> CreateSettingAsync(string name)
  {
    var created = await this[Caller.Owner].PostAsJsonAsync("/api/settings", new { name });
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    return (await JsonAssert.ReadJsonAsync(created)).GetProperty("id").GetGuid();
  }

  /// <summary>
  /// Inserts invites created by the owner, active (expiring in a week) unless
  /// <paramref name="configure"/> changes them.
  /// </summary>
  public async Task<List<SettingInvite>> AddInvitesAsync(
      int count,
      Action<SettingInvite>? configure = null,
      Guid? settingId = null)
  {
    var invites = Enumerable.Range(0, count)
        .Select(_ =>
        {
          var invite = new SettingInvite
          {
            Id = Guid.NewGuid(),
            CampaignSettingId = settingId ?? SettingId,
            CreatedByUserId = UserIds[Caller.Owner],
            Code = InviteCodes.Generate(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
          };
          configure?.Invoke(invite);
          return invite;
        })
        .ToList();

    await _factory.WithDbAsync(db =>
    {
      db.SettingInvites.AddRange(invites);
      return db.SaveChangesAsync();
    });

    return invites;
  }

  private static SettingMembership Membership(Guid settingId, Guid userId, SettingRole role) =>
      new() { Id = Guid.NewGuid(), CampaignSettingId = settingId, UserId = userId, Role = role };
}
