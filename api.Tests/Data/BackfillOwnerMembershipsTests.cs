using Lorebound.Api.Data;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Lorebound.Api.Tests.Data;

/// <summary>
/// Runs the P2-01 data migration on its own database, starting from the
/// migration before it with the rows that existed on dev databases then.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BackfillOwnerMembershipsTests : IAsyncLifetime
{
  private const string MigrationBefore = "20261007162346_FixRelationshipDeleteBehavior";

  private readonly PostgresFixture _fixture;
  private readonly string _connectionString;
  private readonly string _databaseName;

  public BackfillOwnerMembershipsTests(PostgresFixture fixture)
  {
    _fixture = fixture;

    var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString);
    _databaseName = $"{builder.Database}_backfill_test";
    builder.Database = _databaseName;
    builder.Pooling = false;
    _connectionString = builder.ConnectionString;
  }

  public async Task InitializeAsync()
  {
    await AdminAsync($"""DROP DATABASE IF EXISTS "{_databaseName}" WITH (FORCE)""");
    await AdminAsync($"""CREATE DATABASE "{_databaseName}" """);
  }

  public Task DisposeAsync() =>
      AdminAsync($"""DROP DATABASE IF EXISTS "{_databaseName}" WITH (FORCE)""");

  private async Task AdminAsync(string sql)
  {
    await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
  }

  private LoreboundDbContext CreateContext() =>
      new(new DbContextOptionsBuilder<LoreboundDbContext>()
              .UseNpgsql(_connectionString)
              .Options,
          TimeProvider.System);

  private static ApplicationUser User(string name) =>
      new()
      {
        Id = Guid.NewGuid(),
        UserName = $"{name}@example.com",
        NormalizedUserName = $"{name.ToUpperInvariant()}@EXAMPLE.COM",
        Email = $"{name}@example.com",
        NormalizedEmail = $"{name.ToUpperInvariant()}@EXAMPLE.COM",
        DisplayName = name,
        SecurityStamp = Guid.NewGuid().ToString(),
      };

  [Fact]
  public async Task Owners_get_a_GameMaster_row_and_placeholder_dates_are_repaired()
  {
    var noRowOwner = User("norow");
    var playerOwner = User("playerowner");
    var gmOwner = User("gmowner");
    var player = User("player");

    var noRow = new CampaignSetting { Id = Guid.NewGuid(), Name = "No row", OwnerUserId = noRowOwner.Id };
    var demoted = new CampaignSetting { Id = Guid.NewGuid(), Name = "Demoted", OwnerUserId = playerOwner.Id };
    var correct = new CampaignSetting { Id = Guid.NewGuid(), Name = "Correct", OwnerUserId = gmOwner.Id };

    await using (var db = CreateContext())
    {
      await db.GetService<IMigrator>().MigrateAsync(MigrationBefore);

      db.Users.AddRange(noRowOwner, playerOwner, gmOwner, player);
      db.CampaignSettings.AddRange(noRow, demoted, correct);
      await db.SaveChangesAsync();

      // Raw SQL, because rows from that time had '-infinity' dates, which
      // the current model would never write.
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO "SettingMemberships"
              ("Id", "CampaignSettingId", "UserId", "Role", "JoinedAt", "CreatedAt", "UpdatedAt")
          VALUES
              ({Guid.NewGuid()}, {demoted.Id}, {playerOwner.Id}, 'Player', '-infinity', now(), '-infinity'),
              ({Guid.NewGuid()}, {demoted.Id}, {player.Id}, 'Player', '-infinity', now(), '-infinity'),
              ({Guid.NewGuid()}, {correct.Id}, {gmOwner.Id}, 'GameMaster', now(), now(), now())
          """);
    }

    await using (var db = CreateContext())
    {
      await db.Database.MigrateAsync();

      Assert.Empty(await db.Database.GetPendingMigrationsAsync());

      var memberships = await db.SettingMemberships.AsNoTracking().ToListAsync();

      Assert.Equal(4, memberships.Count);
      Assert.All(memberships, m =>
      {
        Assert.True(m.JoinedAt.Year > 2000, "JoinedAt was not repaired");
        Assert.True(m.UpdatedAt.Year > 2000, "UpdatedAt was not repaired");
      });

      SettingRole RoleOf(CampaignSetting setting, ApplicationUser user) =>
          memberships.Single(m => m.CampaignSettingId == setting.Id && m.UserId == user.Id).Role;

      Assert.Equal(SettingRole.GameMaster, RoleOf(noRow, noRowOwner));
      Assert.Equal(SettingRole.GameMaster, RoleOf(demoted, playerOwner));
      Assert.Equal(SettingRole.Player, RoleOf(demoted, player));
      Assert.Equal(SettingRole.GameMaster, RoleOf(correct, gmOwner));
    }
  }
}
