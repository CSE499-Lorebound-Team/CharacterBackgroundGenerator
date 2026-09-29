using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lorebound.Api.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class DatabaseHarnessTests : PostgresTestBase
{
  public DatabaseHarnessTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  [Fact]
  public async Task CreateUserAsync_persists_user_with_CreatedAt()
  {
    var user = await Factory.CreateUserAsync("gm@example.com", "Game Master");

    var saved = await Factory.WithDbAsync(db => db.Users.SingleAsync(u => u.Id == user.Id));

    Assert.Equal("Game Master", saved.DisplayName);
    Assert.NotEqual(default, saved.CreatedAt);
  }

  [Fact]
  public async Task Duplicate_email_is_rejected_by_the_database()
  {
    await Factory.CreateUserAsync("same@example.com");

    var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
        Factory.CreateUserAsync("SAME@example.com", "Someone Else"));

    var postgres = Assert.IsType<PostgresException>(error.InnerException);
    Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
  }

  [Fact]
  public async Task ResetAsync_leaves_tables_empty()
  {
    await Factory.CreateUserAsync();

    await Fixture.ResetAsync();

    Assert.Equal(0, await Factory.WithDbAsync(db => db.Users.CountAsync()));
  }
}
