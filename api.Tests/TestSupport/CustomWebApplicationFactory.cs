using System.Net.Http.Json;
using Lorebound.Api.Data;
using Lorebound.Api.Models;
using Lorebound.Api.Security;
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

  /// <summary>
  /// Every client sends the CSRF header like the real frontend, so tests
  /// exercise endpoints rather than the CSRF check; CsrfTests removes it.
  /// </summary>
  protected override void ConfigureClient(HttpClient client)
  {
    base.ConfigureClient(client);
    client.DefaultRequestHeaders.Add(
        CsrfProtectionMiddleware.HeaderName, CsrfProtectionMiddleware.HeaderValue);
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);

    // Every test client shares one IP partition, so lift the auth rate limit;
    // RateLimitTests restores it with WithConfig.
    builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");

    // Test-only controllers (routes under /test), e.g. a protected route to
    // prove a login cookie authenticates.
    builder.ConfigureServices(services =>
    {
      services
          .AddControllers()
          .AddApplicationPart(typeof(CustomWebApplicationFactory).Assembly);

      // One shared instance, so copies made with WithConfig record here too.
      services.AddSingleton<IEmailSender<ApplicationUser>>(Emails);
    });
  }

  /// <summary>Emails the API "sent" during the current test.</summary>
  public TestEmailSender Emails { get; } = new();

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

  /// <summary>
  /// Creates a confirmed user and signs it in through POST /api/auth/login.
  /// The client keeps the auth cookie, so later calls are authenticated.
  /// </summary>
  public async Task<(HttpClient Client, ApplicationUser User)> CreateSignedInClientAsync(
      string email = "player@example.com",
      string displayName = "Player")
  {
    var user = await CreateUserAsync(email, displayName);
    var client = this.CreateCookieClient();

    var response = await client.PostAsJsonAsync(
        "/api/auth/login", new { email, password = DefaultPassword });
    response.EnsureSuccessStatusCode();

    return (client, user);
  }

  /// <summary>Runs <paramref name="action"/> with a fresh DbContext.</summary>
  public async Task<T> WithDbAsync<T>(Func<LoreboundDbContext, Task<T>> action)
  {
    using var scope = Services.CreateScope();
    return await action(scope.ServiceProvider.GetRequiredService<LoreboundDbContext>());
  }
}
