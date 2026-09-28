using Lorebound.Api.Data;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.TestSupport;

/// <summary>
/// Hosts the API against the real Postgres from <see cref="PostgresFixture"/>.
/// Get it from the fixture rather than constructing it; tests that do not need
/// a database use <see cref="ApiFactory"/> instead.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
  private readonly string _connectionString;

  public CustomWebApplicationFactory(string connectionString)
  {
    _connectionString = connectionString;
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
  }

  /// <summary>
  /// A client that stores and resends cookies like a browser, for cookie auth.
  /// </summary>
  public HttpClient CreateCookieClient() =>
      CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

  /// <summary>
  /// Inserts a user directly. It has no password until Identity is registered
  /// (P1-01); switch this to UserManager then.
  /// </summary>
  public async Task<ApplicationUser> CreateUserAsync(
      string email = "player@example.com",
      string displayName = "Player")
  {
    using var scope = Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LoreboundDbContext>();

    var user = new ApplicationUser
    {
      Id = Guid.NewGuid(),
      UserName = email,
      NormalizedUserName = email.ToUpperInvariant(),
      Email = email,
      NormalizedEmail = email.ToUpperInvariant(),
      DisplayName = displayName,
      SecurityStamp = Guid.NewGuid().ToString(),
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();
    return user;
  }

  /// <summary>Runs <paramref name="action"/> with a fresh DbContext.</summary>
  public async Task<T> WithDbAsync<T>(Func<LoreboundDbContext, Task<T>> action)
  {
    using var scope = Services.CreateScope();
    return await action(scope.ServiceProvider.GetRequiredService<LoreboundDbContext>());
  }
}
