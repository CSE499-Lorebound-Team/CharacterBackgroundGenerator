using Lorebound.Api.Data;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
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

    // Test-only controllers (routes under /test), e.g. a protected route to
    // prove a login cookie authenticates.
    builder.ConfigureServices(services =>
        services
            .AddControllers()
            .AddApplicationPart(typeof(CustomWebApplicationFactory).Assembly));
  }

  public const string DefaultPassword = "correct horse battery";

  /// <summary>
  /// Creates a confirmed user through UserManager, so Identity's password
  /// policy and unique-email check apply. Throws if Identity rejects it.
  /// </summary>
  public async Task<ApplicationUser> CreateUserAsync(
      string email = "player@example.com",
      string displayName = "Player",
      string password = DefaultPassword)
  {
    using var scope = Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    var user = new ApplicationUser
    {
      UserName = email,
      Email = email,
      EmailConfirmed = true,
      DisplayName = displayName,
    };

    var result = await userManager.CreateAsync(user, password);
    if (!result.Succeeded)
    {
      throw new InvalidOperationException(
          "Identity rejected the test user: " +
          string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    return user;
  }

  /// <summary>Runs <paramref name="action"/> with a fresh DbContext.</summary>
  public async Task<T> WithDbAsync<T>(Func<LoreboundDbContext, Task<T>> action)
  {
    using var scope = Services.CreateScope();
    return await action(scope.ServiceProvider.GetRequiredService<LoreboundDbContext>());
  }
}
