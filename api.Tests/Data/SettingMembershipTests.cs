using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lorebound.Api.Tests.Data;

// P2-01: the membership table's constraints, checked against real Postgres.
[Collection(PostgresCollection.Name)]
public class SettingMembershipTests : PostgresTestBase
{
  public SettingMembershipTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private async Task<(ApplicationUser Owner, CampaignSetting Setting)> CreateSettingAsync()
  {
    var owner = await Factory.CreateUserAsync("owner@example.com", "Owner");

    var setting = new CampaignSetting
    {
      Id = Guid.NewGuid(),
      Name = "Eberron",
      OwnerUserId = owner.Id,
    };

    await Factory.WithDbAsync(db =>
    {
      db.CampaignSettings.Add(setting);
      db.SettingMemberships.Add(Membership(setting.Id, owner.Id, SettingRole.GameMaster));
      return db.SaveChangesAsync();
    });

    return (owner, setting);
  }

  private static SettingMembership Membership(Guid settingId, Guid userId, SettingRole role) =>
      new()
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = settingId,
        UserId = userId,
        Role = role,
      };

  [Fact]
  public async Task Duplicate_setting_and_user_violates_the_unique_index()
  {
    var (owner, setting) = await CreateSettingAsync();

    var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
        Factory.WithDbAsync(db =>
        {
          db.SettingMemberships.Add(Membership(setting.Id, owner.Id, SettingRole.Player));
          return db.SaveChangesAsync();
        }));

    var postgres = Assert.IsType<PostgresException>(error.InnerException);
    Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    Assert.Equal("IX_SettingMemberships_CampaignSettingId_UserId", postgres.ConstraintName);
  }

  [Fact]
  public async Task Same_user_can_belong_to_several_settings()
  {
    var (owner, _) = await CreateSettingAsync();

    var other = new CampaignSetting { Id = Guid.NewGuid(), Name = "Krynn", OwnerUserId = owner.Id };
    await Factory.WithDbAsync(db =>
    {
      db.CampaignSettings.Add(other);
      db.SettingMemberships.Add(Membership(other.Id, owner.Id, SettingRole.GameMaster));
      return db.SaveChangesAsync();
    });

    Assert.Equal(2, await Factory.WithDbAsync(db =>
        db.SettingMemberships.CountAsync(m => m.UserId == owner.Id)));
  }

  [Fact]
  public async Task Role_is_stored_as_its_name()
  {
    await CreateSettingAsync();

    var roles = await Factory.WithDbAsync(db =>
        db.Database
            .SqlQueryRaw<string>("""SELECT "Role" AS "Value" FROM "SettingMemberships" """)
            .ToListAsync());

    Assert.Equal(["GameMaster"], roles);
  }

  [Fact]
  public async Task JoinedAt_and_timestamps_default_to_the_save_time()
  {
    var before = DateTimeOffset.UtcNow.AddSeconds(-5);
    var (owner, setting) = await CreateSettingAsync();

    var membership = await Factory.WithDbAsync(db =>
        db.SettingMemberships.SingleAsync(m =>
            m.CampaignSettingId == setting.Id && m.UserId == owner.Id));

    Assert.True(membership.JoinedAt > before);
    Assert.True(membership.CreatedAt > before);
    Assert.True(membership.UpdatedAt > before);
  }

  [Fact]
  public async Task An_explicit_JoinedAt_is_kept()
  {
    var (_, setting) = await CreateSettingAsync();
    var player = await Factory.CreateUserAsync("player@example.com", "Player");
    var joinedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    await Factory.WithDbAsync(db =>
    {
      var membership = Membership(setting.Id, player.Id, SettingRole.Player);
      membership.JoinedAt = joinedAt;
      db.SettingMemberships.Add(membership);
      return db.SaveChangesAsync();
    });

    Assert.Equal(joinedAt, await Factory.WithDbAsync(db =>
        db.SettingMemberships
            .Where(m => m.UserId == player.Id)
            .Select(m => m.JoinedAt)
            .SingleAsync()));
  }

  [Fact]
  public async Task Deleting_a_setting_deletes_its_memberships()
  {
    var (_, setting) = await CreateSettingAsync();
    var player = await Factory.CreateUserAsync("player@example.com", "Player");
    await Factory.WithDbAsync(db =>
    {
      db.SettingMemberships.Add(Membership(setting.Id, player.Id, SettingRole.Player));
      return db.SaveChangesAsync();
    });

    // A fresh context with nothing loaded, so the database does the cascade.
    await Factory.WithDbAsync(db =>
        db.CampaignSettings.Where(s => s.Id == setting.Id).ExecuteDeleteAsync());

    Assert.Equal(0, await Factory.WithDbAsync(db => db.SettingMemberships.CountAsync()));
    Assert.Equal(2, await Factory.WithDbAsync(db => db.Users.CountAsync()));
  }

  [Fact]
  public async Task A_user_with_memberships_cannot_be_deleted()
  {
    var (_, setting) = await CreateSettingAsync();
    var player = await Factory.CreateUserAsync("player@example.com", "Player");
    await Factory.WithDbAsync(db =>
    {
      db.SettingMemberships.Add(Membership(setting.Id, player.Id, SettingRole.Player));
      return db.SaveChangesAsync();
    });

    var error = await Assert.ThrowsAsync<PostgresException>(() =>
        Factory.WithDbAsync(db =>
            db.Users.Where(u => u.Id == player.Id).ExecuteDeleteAsync()));

    Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    Assert.Equal("FK_SettingMemberships_AspNetUsers_UserId", error.ConstraintName);
  }
}
