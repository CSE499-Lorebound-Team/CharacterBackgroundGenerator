using Lorebound.Api.Models;
using Lorebound.Api.Sharing;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lorebound.Api.Tests.Data;

// P3-01: the invite table's constraints, checked against real Postgres.
[Collection(PostgresCollection.Name)]
public class SettingInviteTests : PostgresTestBase
{
  public SettingInviteTests(PostgresFixture fixture)
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
      return db.SaveChangesAsync();
    });

    return (owner, setting);
  }

  private static SettingInvite Invite(Guid settingId, Guid createdBy, string? code = null) =>
      new()
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = settingId,
        CreatedByUserId = createdBy,
        Code = code ?? InviteCodes.Generate(),
      };

  private Task<int> AddAsync(SettingInvite invite) =>
      Factory.WithDbAsync(db =>
      {
        db.SettingInvites.Add(invite);
        return db.SaveChangesAsync();
      });

  [Fact]
  public async Task Duplicate_code_violates_the_unique_index()
  {
    var (owner, setting) = await CreateSettingAsync();
    await AddAsync(Invite(setting.Id, owner.Id, "ABCDEFGH23"));

    var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
        AddAsync(Invite(setting.Id, owner.Id, "ABCDEFGH23")));

    var postgres = Assert.IsType<PostgresException>(error.InnerException);
    Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    Assert.Equal("IX_SettingInvites_Code", postgres.ConstraintName);
  }

  [Fact]
  public async Task New_invite_starts_unused_with_CreatedAt_set()
  {
    var before = DateTimeOffset.UtcNow.AddSeconds(-5);
    var (owner, setting) = await CreateSettingAsync();
    var invite = Invite(setting.Id, owner.Id);
    await AddAsync(invite);

    var saved = await Factory.WithDbAsync(db => db.SettingInvites.SingleAsync());

    Assert.Equal(invite.Code, saved.Code);
    Assert.Equal(0, saved.UseCount);
    Assert.Null(saved.ExpiresAt);
    Assert.Null(saved.MaxUses);
    Assert.Null(saved.RevokedAt);
    Assert.True(saved.CreatedAt > before);
  }

  [Fact]
  public async Task UseCount_cannot_go_past_MaxUses()
  {
    var (owner, setting) = await CreateSettingAsync();
    var invite = Invite(setting.Id, owner.Id);
    invite.MaxUses = 1;
    await AddAsync(invite);

    await Factory.WithDbAsync(db => db.SettingInvites
        .ExecuteUpdateAsync(set => set.SetProperty(i => i.UseCount, i => i.UseCount + 1)));

    var error = await Assert.ThrowsAsync<PostgresException>(() =>
        Factory.WithDbAsync(db => db.SettingInvites
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.UseCount, i => i.UseCount + 1))));

    Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    Assert.Equal("CK_SettingInvites_UseCount", error.ConstraintName);
  }

  [Fact]
  public async Task MaxUses_must_be_positive()
  {
    var (owner, setting) = await CreateSettingAsync();
    var invite = Invite(setting.Id, owner.Id);
    invite.MaxUses = 0;

    var error = await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(invite));

    var postgres = Assert.IsType<PostgresException>(error.InnerException);
    Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
    Assert.Equal("CK_SettingInvites_MaxUses", postgres.ConstraintName);
  }

  [Fact]
  public async Task Deleting_a_setting_deletes_its_invites()
  {
    var (owner, setting) = await CreateSettingAsync();
    await AddAsync(Invite(setting.Id, owner.Id));

    // A fresh context with nothing loaded, so the database does the cascade.
    await Factory.WithDbAsync(db =>
        db.CampaignSettings.Where(s => s.Id == setting.Id).ExecuteDeleteAsync());

    Assert.Equal(0, await Factory.WithDbAsync(db => db.SettingInvites.CountAsync()));
  }

  [Fact]
  public async Task A_user_who_created_invites_cannot_be_deleted()
  {
    var (_, setting) = await CreateSettingAsync();
    var gm = await Factory.CreateUserAsync("gm@example.com", "GM");
    await AddAsync(Invite(setting.Id, gm.Id));

    var error = await Assert.ThrowsAsync<PostgresException>(() =>
        Factory.WithDbAsync(db =>
            db.Users.Where(u => u.Id == gm.Id).ExecuteDeleteAsync()));

    Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    Assert.Equal("FK_SettingInvites_AspNetUsers_CreatedByUserId", error.ConstraintName);
  }
}
