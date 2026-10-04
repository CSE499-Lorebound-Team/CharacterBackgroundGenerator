using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
  public async Task CreateUserAsync_sets_a_password_identity_accepts()
  {
    var user = await Factory.CreateUserAsync();

    using var scope = Factory.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    Assert.True(await userManager.CheckPasswordAsync(
        user, CustomWebApplicationFactory.DefaultPassword));
  }

  [Fact]
  public async Task Password_shorter_than_10_characters_is_rejected()
  {
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        Factory.CreateUserAsync(password: "123456789"));
  }

  [Fact]
  public async Task Duplicate_email_is_rejected_by_the_database()
  {
    await Factory.CreateUserAsync("same@example.com");

    // Bypass UserManager, whose own unique-email check would reject it first.
    var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
        Factory.WithDbAsync(db =>
        {
          db.Users.Add(new ApplicationUser
          {
            UserName = "other",
            NormalizedUserName = "OTHER",
            Email = "SAME@example.com",
            NormalizedEmail = "SAME@EXAMPLE.COM",
            DisplayName = "Someone Else",
          });
          return db.SaveChangesAsync();
        }));

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
