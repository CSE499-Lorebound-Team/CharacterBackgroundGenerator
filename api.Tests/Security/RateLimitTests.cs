using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Security;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.Security;

[Collection(PostgresCollection.Name)]
public class RateLimitTests : PostgresTestBase
{
  public RateLimitTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  /// <summary>A factory with the production limit (10 per minute).</summary>
  private WebApplicationFactory<Program> LimitedFactory() =>
      Factory.WithConfig("RateLimiting:Auth:PermitLimit", "10");

  private static Task<HttpResponseMessage> LoginAsync(HttpClient client) =>
      client.PostAsJsonAsync("/api/auth/login",
          new { email = "nobody@example.com", password = "not the password" });

  [Fact]
  public void Auth_policy_covers_exactly_the_credential_and_email_endpoints()
  {
    var limited = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
        .OfType<RouteEndpoint>()
        .Where(endpoint => endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            == RateLimitingSetup.AuthPolicy)
        .Select(endpoint => endpoint.RoutePattern.RawText)
        .Order();

    Assert.Equal(
        ["api/auth/forgot-password", "api/auth/login", "api/auth/register", "api/auth/resend-confirmation"],
        limited);
  }

  [Fact]
  public async Task Eleventh_login_in_a_minute_returns_429_problem_json()
  {
    var client = LimitedFactory().CreateCookieClient();

    for (var attempt = 1; attempt <= 10; attempt++)
    {
      Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client)).StatusCode);
    }

    var response = await LoginAsync(client);

    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    var retryAfter = int.Parse(response.Headers.GetValues("Retry-After").Single());
    Assert.InRange(retryAfter, 1, 60);

    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(429, problem.GetProperty("status").GetInt32());
    Assert.Equal(retryAfter, problem.GetProperty("retryAfterSeconds").GetInt32());
    Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
  }

  [Fact]
  public async Task The_limit_is_shared_across_the_auth_endpoints()
  {
    var client = LimitedFactory().CreateCookieClient();

    for (var attempt = 1; attempt <= 10; attempt++)
    {
      await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@example.com" });
    }

    Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(client)).StatusCode);
  }

  [Fact]
  public void Invite_policy_covers_the_code_lookup_endpoints()
  {
    var limited = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
        .OfType<RouteEndpoint>()
        .Where(endpoint => endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            == RateLimitingSetup.InvitePolicy)
        .Select(endpoint => endpoint.RoutePattern.RawText)
        .Order();

    Assert.Equal(["api/invites/{code}", "api/invites/{code}/accept"], limited);
  }

  // Signs in on a client of the limited factory (one login: the auth counter,
  // not the invite one).
  private async Task<HttpClient> SignedInLimitedClientAsync()
  {
    await Factory.CreateUserAsync("guesser@example.com", "Guesser");
    var client = LimitedFactory().CreateCookieClient();
    var login = await client.PostAsJsonAsync("/api/auth/login",
        new { email = "guesser@example.com", password = CustomWebApplicationFactory.DefaultPassword });
    login.EnsureSuccessStatusCode();
    return client;
  }

  [Fact]
  public async Task Eleventh_invite_preview_in_a_minute_returns_429()
  {
    var client = await SignedInLimitedClientAsync();

    for (var attempt = 1; attempt <= 10; attempt++)
    {
      Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/invites/ZZZZZZZZZZ")).StatusCode);
    }

    var response = await client.GetAsync("/api/invites/ZZZZZZZZZZ");

    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
  }

  // Preview and accept share one counter, so alternating between them does
  // not double the number of guesses.
  [Fact]
  public async Task Preview_and_accept_share_the_invite_limit()
  {
    var client = await SignedInLimitedClientAsync();

    for (var attempt = 1; attempt <= 5; attempt++)
    {
      await client.GetAsync("/api/invites/ZZZZZZZZZZ");
      await client.PostAsync("/api/invites/ZZZZZZZZZZ/accept", null);
    }

    var response = await client.PostAsync("/api/invites/ZZZZZZZZZZ/accept", null);

    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
  }

  [Fact]
  public async Task Invite_and_login_limits_are_counted_separately()
  {
    var client = await SignedInLimitedClientAsync();

    for (var attempt = 1; attempt <= 10; attempt++)
    {
      await client.GetAsync("/api/invites/ZZZZZZZZZZ");
    }

    // Login has used 1 of its 10; the invite counter is spent.
    Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client)).StatusCode);
    Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/invites/ZZZZZZZZZZ")).StatusCode);
  }

  [Fact]
  public async Task Other_endpoints_are_not_limited()
  {
    var client = LimitedFactory().CreateCookieClient();
    for (var attempt = 1; attempt <= 11; attempt++)
    {
      await LoginAsync(client);
    }

    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    // Reaches the action (400 for a bad link) instead of being throttled.
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
        "/api/auth/confirm-email", new { userId = Guid.NewGuid(), code = "x" })).StatusCode);
  }
}
