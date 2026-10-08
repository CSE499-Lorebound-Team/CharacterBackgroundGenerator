using System.Data.Common;
using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Errors;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.Auth;

// P2-02: every caller x method combination of ISettingAccess, on real Postgres.
[Collection(PostgresCollection.Name)]
public class SettingAccessTests : PostgresTestBase
{
  public SettingAccessTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  public enum Caller
  {
    Owner,
    GameMaster,
    Player,
    NonMember,
  }

  private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
  {
    public Guid UserId => userId ?? throw new UnauthorizedAccessException("No user is signed in.");

    public string Email => string.Empty;

    public string DisplayName => string.Empty;
  }

  private sealed record Seeded(Guid SettingId, Dictionary<Caller, Guid> Users);

  private async Task<Seeded> SeedAsync(bool ownerHasMembershipRow = true)
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
      OwnerUserId = users[Caller.Owner],
    };

    await Factory.WithDbAsync(db =>
    {
      db.CampaignSettings.Add(setting);
      if (ownerHasMembershipRow)
      {
        db.SettingMemberships.Add(Membership(setting.Id, users[Caller.Owner], SettingRole.GameMaster));
      }

      db.SettingMemberships.Add(Membership(setting.Id, users[Caller.GameMaster], SettingRole.GameMaster));
      db.SettingMemberships.Add(Membership(setting.Id, users[Caller.Player], SettingRole.Player));
      return db.SaveChangesAsync();
    });

    return new Seeded(setting.Id, users);
  }

  private static SettingMembership Membership(Guid settingId, Guid userId, SettingRole role) =>
      new() { Id = Guid.NewGuid(), CampaignSettingId = settingId, UserId = userId, Role = role };

  private async Task<T> AsAsync<T>(Guid? userId, Func<ISettingAccess, LoreboundDbContext, Task<T>> action)
  {
    using var scope = Factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LoreboundDbContext>();
    return await action(new SettingAccess(db, new FakeCurrentUser(userId)), db);
  }

  private Task<T> AsAsync<T>(Seeded seeded, Caller caller, Func<ISettingAccess, Task<T>> action) =>
      AsAsync(seeded.Users[caller], (access, _) => action(access));

  // "ok", "404" or "403", so one table describes the whole matrix.
  private static async Task<string> OutcomeAsync(Func<Task<CampaignSetting>> call, Guid expectedId)
  {
    try
    {
      var setting = await call();
      Assert.Equal(expectedId, setting.Id);
      return "ok";
    }
    catch (NotFoundException)
    {
      return "404";
    }
    catch (ForbiddenException)
    {
      return "403";
    }
  }

  [Theory]
  [InlineData(Caller.Owner, SettingRole.GameMaster)]
  [InlineData(Caller.GameMaster, SettingRole.GameMaster)]
  [InlineData(Caller.Player, SettingRole.Player)]
  [InlineData(Caller.NonMember, null)]
  public async Task GetRoleAsync_returns_the_callers_role(Caller caller, SettingRole? expected)
  {
    var seeded = await SeedAsync();

    var role = await AsAsync(seeded, caller, access => access.GetRoleAsync(seeded.SettingId));

    Assert.Equal(expected, role);
  }

  [Theory]
  [InlineData(Caller.Owner, "ok")]
  [InlineData(Caller.GameMaster, "ok")]
  [InlineData(Caller.Player, "ok")]
  [InlineData(Caller.NonMember, "404")]
  public async Task RequireMemberAsync(Caller caller, string expected)
  {
    var seeded = await SeedAsync();

    var outcome = await AsAsync(seeded, caller, access =>
        OutcomeAsync(() => access.RequireMemberAsync(seeded.SettingId), seeded.SettingId));

    Assert.Equal(expected, outcome);
  }

  [Theory]
  [InlineData(Caller.Owner, "ok")]
  [InlineData(Caller.GameMaster, "ok")]
  [InlineData(Caller.Player, "403")]
  [InlineData(Caller.NonMember, "404")]
  public async Task RequireGameMasterAsync(Caller caller, string expected)
  {
    var seeded = await SeedAsync();

    var outcome = await AsAsync(seeded, caller, access =>
        OutcomeAsync(() => access.RequireGameMasterAsync(seeded.SettingId), seeded.SettingId));

    Assert.Equal(expected, outcome);
  }

  [Theory]
  [InlineData(Caller.Owner, "ok")]
  [InlineData(Caller.GameMaster, "403")]
  [InlineData(Caller.Player, "403")]
  [InlineData(Caller.NonMember, "404")]
  public async Task RequireOwnerAsync(Caller caller, string expected)
  {
    var seeded = await SeedAsync();

    var outcome = await AsAsync(seeded, caller, access =>
        OutcomeAsync(() => access.RequireOwnerAsync(seeded.SettingId), seeded.SettingId));

    Assert.Equal(expected, outcome);
  }

  [Theory]
  [InlineData(Caller.Owner, true)]
  [InlineData(Caller.GameMaster, true)]
  [InlineData(Caller.Player, true)]
  [InlineData(Caller.NonMember, false)]
  public async Task VisibleToCurrentUser_includes_only_member_settings(Caller caller, bool visible)
  {
    var seeded = await SeedAsync();

    var ids = await AsAsync(seeded, caller, access =>
        access.VisibleToCurrentUser().Select(setting => setting.Id).ToListAsync());

    Assert.Equal(visible ? [seeded.SettingId] : [], ids);
  }

  [Fact]
  public async Task A_missing_setting_is_404_for_every_require_method_and_has_no_role()
  {
    var seeded = await SeedAsync();
    var missing = Guid.NewGuid();

    await AsAsync(seeded, Caller.Owner, async access =>
    {
      Assert.Null(await access.GetRoleAsync(missing));
      await Assert.ThrowsAsync<NotFoundException>(() => access.RequireMemberAsync(missing));
      await Assert.ThrowsAsync<NotFoundException>(() => access.RequireGameMasterAsync(missing));
      await Assert.ThrowsAsync<NotFoundException>(() => access.RequireOwnerAsync(missing));
      return true;
    });
  }

  [Fact]
  public async Task Owner_without_a_membership_row_is_still_GameMaster_and_owner()
  {
    var seeded = await SeedAsync(ownerHasMembershipRow: false);

    await AsAsync(seeded, Caller.Owner, async access =>
    {
      Assert.Equal(SettingRole.GameMaster, await access.GetRoleAsync(seeded.SettingId));
      Assert.Equal(seeded.SettingId, (await access.RequireOwnerAsync(seeded.SettingId)).Id);
      Assert.Equal(seeded.SettingId, (await access.RequireGameMasterAsync(seeded.SettingId)).Id);
      Assert.Equal(1, await access.VisibleToCurrentUser().CountAsync());
      return true;
    });
  }

  [Fact]
  public async Task Anonymous_callers_are_rejected_before_any_lookup()
  {
    var seeded = await SeedAsync();

    await AsAsync<bool>(null, async (access, _) =>
    {
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.GetRoleAsync(seeded.SettingId));
      await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.RequireMemberAsync(seeded.SettingId));
      Assert.Throws<UnauthorizedAccessException>(() => access.VisibleToCurrentUser());
      return true;
    });
  }

  [Fact]
  public async Task Returned_setting_is_tracked_so_changes_can_be_saved()
  {
    var seeded = await SeedAsync();

    await AsAsync<int>(seeded.Users[Caller.GameMaster], async (access, db) =>
    {
      var setting = await access.RequireGameMasterAsync(seeded.SettingId);
      setting.Name = "Renamed";
      return await db.SaveChangesAsync();
    });

    Assert.Equal("Renamed", await Factory.WithDbAsync(db =>
        db.CampaignSettings.Select(s => s.Name).SingleAsync()));
  }

  [Fact]
  public async Task Role_check_is_a_single_query()
  {
    var seeded = await SeedAsync();
    var counter = new CommandCounter();

    var options = new DbContextOptionsBuilder<LoreboundDbContext>()
        .UseNpgsql(Fixture.ConnectionString)
        .AddInterceptors(counter)
        .Options;
    await using var db = new LoreboundDbContext(options, TimeProvider.System);
    var access = new SettingAccess(db, new FakeCurrentUser(seeded.Users[Caller.Player]));

    await Assert.ThrowsAsync<ForbiddenException>(() => access.RequireGameMasterAsync(seeded.SettingId));

    Assert.Equal(1, counter.Count);
  }

  private sealed class CommandCounter : DbCommandInterceptor
  {
    public int Count { get; private set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
      Count++;
      return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
  }
}
