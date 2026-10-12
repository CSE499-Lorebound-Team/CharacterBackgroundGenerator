using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Errors;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.Auth;

// P6-02: every caller x method combination of ICharacterAccess, on real
// Postgres. The character belongs to Owner, a Player of the setting.
[Collection(PostgresCollection.Name)]
public class CharacterAccessTests : PostgresTestBase
{
  public CharacterAccessTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  public enum Caller
  {
    Owner,
    SettingOwner,
    GameMaster,
    OtherPlayer,
    NonMember,
  }

  private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
  {
    public Guid UserId => userId;

    public string Email => string.Empty;

    public string DisplayName => string.Empty;
  }

  private sealed record Seeded(Guid SettingId, Guid CharacterId, Dictionary<Caller, Guid> Users);

  private async Task<Seeded> SeedAsync(bool ownerIsMember = true)
  {
    var users = new Dictionary<Caller, Guid>();
    foreach (var caller in Enum.GetValues<Caller>())
    {
      var user = await Factory.CreateUserAsync($"{caller}@example.com".ToLowerInvariant(), caller.ToString());
      users[caller] = user.Id;
    }

    var setting = new CampaignSetting
    {
      Id = Guid.NewGuid(),
      Name = "Eberron",
      OwnerUserId = users[Caller.SettingOwner],
    };
    var character = new Character
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = setting.Id,
      OwnerUserId = users[Caller.Owner],
      Name = "Aster",
    };

    await Factory.WithDbAsync(db =>
    {
      db.CampaignSettings.Add(setting);
      db.SettingMemberships.Add(Membership(setting.Id, users[Caller.SettingOwner], SettingRole.GameMaster));
      db.SettingMemberships.Add(Membership(setting.Id, users[Caller.GameMaster], SettingRole.GameMaster));
      db.SettingMemberships.Add(Membership(setting.Id, users[Caller.OtherPlayer], SettingRole.Player));
      if (ownerIsMember)
      {
        db.SettingMemberships.Add(Membership(setting.Id, users[Caller.Owner], SettingRole.Player));
      }

      db.Characters.Add(character);
      return db.SaveChangesAsync();
    });

    return new Seeded(setting.Id, character.Id, users);
  }

  private static SettingMembership Membership(Guid settingId, Guid userId, SettingRole role) =>
      new() { Id = Guid.NewGuid(), CampaignSettingId = settingId, UserId = userId, Role = role };

  private async Task<T> AsAsync<T>(Guid userId, Func<ICharacterAccess, Task<T>> action)
  {
    using var scope = Factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LoreboundDbContext>();
    return await action(new CharacterAccess(db, new FakeCurrentUser(userId)));
  }

  private Task<string> OutcomeAsync(
      Seeded seeded,
      Caller caller,
      Func<ICharacterAccess, Guid, Task<CharacterAccessResult>> call,
      Guid? characterId = null) =>
      AsAsync(seeded.Users[caller], async access =>
      {
        var id = characterId ?? seeded.CharacterId;
        try
        {
          var result = await call(access, id);
          Assert.Equal(id, result.Character.Id);
          return Describe(result);
        }
        catch (NotFoundException)
        {
          return "404";
        }
        catch (ForbiddenException)
        {
          return "403";
        }
      });

  // "write", "read-only" (with "owner" when the caller owns it), "404" or
  // "403", so one table describes the whole matrix.
  private static string Describe(CharacterAccessResult result) =>
      (result.IsOwner ? "owner " : string.Empty) + (result.IsReadOnly ? "read-only" : "write");

  [Theory]
  [InlineData(Caller.Owner, true, "owner write")]
  [InlineData(Caller.Owner, false, "owner read-only")]
  [InlineData(Caller.SettingOwner, true, "read-only")]
  [InlineData(Caller.GameMaster, true, "read-only")]
  [InlineData(Caller.OtherPlayer, true, "404")]
  [InlineData(Caller.NonMember, true, "404")]
  public async Task RequireReadAsync(Caller caller, bool ownerIsMember, string expected)
  {
    var seeded = await SeedAsync(ownerIsMember);

    var outcome = await OutcomeAsync(seeded, caller, (access, id) => access.RequireReadAsync(id));

    Assert.Equal(expected, outcome);
  }

  [Theory]
  [InlineData(Caller.Owner, true, "owner write")]
  [InlineData(Caller.Owner, false, "403")]
  [InlineData(Caller.SettingOwner, true, "403")]
  [InlineData(Caller.GameMaster, true, "403")]
  [InlineData(Caller.OtherPlayer, true, "404")]
  [InlineData(Caller.NonMember, true, "404")]
  public async Task RequireWriteAsync(Caller caller, bool ownerIsMember, string expected)
  {
    var seeded = await SeedAsync(ownerIsMember);

    var outcome = await OutcomeAsync(seeded, caller, (access, id) => access.RequireWriteAsync(id));

    Assert.Equal(expected, outcome);
  }

  [Theory]
  [InlineData(Caller.Owner, true, "owner write")]
  [InlineData(Caller.Owner, false, "owner read-only")]
  [InlineData(Caller.SettingOwner, true, "403")]
  [InlineData(Caller.GameMaster, true, "403")]
  [InlineData(Caller.OtherPlayer, true, "404")]
  [InlineData(Caller.NonMember, true, "404")]
  public async Task RequireOwnerAsync(Caller caller, bool ownerIsMember, string expected)
  {
    var seeded = await SeedAsync(ownerIsMember);

    var outcome = await OutcomeAsync(seeded, caller, (access, id) => access.RequireOwnerAsync(id));

    Assert.Equal(expected, outcome);
  }

  [Fact]
  public async Task A_missing_character_is_404_for_everyone()
  {
    var seeded = await SeedAsync();

    foreach (var caller in Enum.GetValues<Caller>())
    {
      Assert.Equal("404", await OutcomeAsync(
          seeded, caller, (access, id) => access.RequireReadAsync(id), Guid.NewGuid()));
    }
  }

  [Fact]
  public async Task A_read_only_owner_gets_a_clear_reason_on_write()
  {
    var seeded = await SeedAsync(ownerIsMember: false);

    var error = await AsAsync(seeded.Users[Caller.Owner], access =>
        Assert.ThrowsAsync<ForbiddenException>(() => access.RequireWriteAsync(seeded.CharacterId)));

    Assert.Equal(
        "This character is read-only because you are no longer a member of its setting.",
        error.Message);
  }

  // A GameMaster's own character (an NPC or their PC) is an owner-member.
  [Fact]
  public async Task A_GameMaster_can_write_their_own_character()
  {
    var seeded = await SeedAsync();
    var npcId = Guid.NewGuid();
    await Factory.WithDbAsync(db =>
    {
      db.Characters.Add(new Character
      {
        Id = npcId,
        CampaignSettingId = seeded.SettingId,
        OwnerUserId = seeded.Users[Caller.SettingOwner],
      });
      return db.SaveChangesAsync();
    });

    var outcome = await OutcomeAsync(
        seeded, Caller.SettingOwner, (access, id) => access.RequireWriteAsync(id), npcId);

    Assert.Equal("owner write", outcome);
  }

  [Fact]
  public async Task Rejoining_the_setting_restores_write_access()
  {
    var seeded = await SeedAsync(ownerIsMember: false);
    Assert.Equal("403", await OutcomeAsync(seeded, Caller.Owner, (access, id) => access.RequireWriteAsync(id)));

    await Factory.WithDbAsync(db =>
    {
      db.SettingMemberships.Add(Membership(seeded.SettingId, seeded.Users[Caller.Owner], SettingRole.Player));
      return db.SaveChangesAsync();
    });

    Assert.Equal("owner write", await OutcomeAsync(seeded, Caller.Owner, (access, id) => access.RequireWriteAsync(id)));
  }

  [Fact]
  public async Task The_returned_character_is_tracked()
  {
    var seeded = await SeedAsync();

    // Endpoints edit the returned character and save, so it must be tracked.
    using var scope = Factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LoreboundDbContext>();
    var access = new CharacterAccess(db, new FakeCurrentUser(seeded.Users[Caller.Owner]));
    var tracked = await access.RequireWriteAsync(seeded.CharacterId);
    tracked.Character.Name = "Renamed";
    await db.SaveChangesAsync();

    Assert.Equal("Renamed", await Factory.WithDbAsync(d =>
        d.Characters.Where(c => c.Id == seeded.CharacterId).Select(c => c.Name).SingleAsync()));
  }
}
