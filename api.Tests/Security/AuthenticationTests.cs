using System.Net;
using System.Text.Json;
using Lorebound.Api.Auth;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lorebound.Api.Tests.Security;

public class AuthenticationTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;

  public AuthenticationTests(ApiFactory factory)
  {
    _factory = factory;
  }

  // The cookie is Secure, so the client must use https to send it back.
  private HttpClient CreateHttpsClient() =>
      _factory.CreateClient(new WebApplicationFactoryClientOptions
      {
        BaseAddress = new Uri("https://localhost"),
      });

  private static async Task AssertProblemAsync(
      HttpResponseMessage response, HttpStatusCode expected)
  {
    Assert.Equal(expected, response.StatusCode);
    Assert.Null(response.Headers.Location);
    Assert.Equal(
        "application/problem+json",
        response.Content.Headers.ContentType?.MediaType);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.Equal((int)expected, json.RootElement.GetProperty("status").GetInt32());
    Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("traceId").GetString()));
  }

  private static string AuthCookie(HttpResponseMessage response) =>
      response.Headers.GetValues("Set-Cookie")
          .Single(c => c.StartsWith(AuthenticationSetup.CookieName + "="));

  [Fact]
  public async Task Anonymous_call_to_protected_route_returns_401_problem_json()
  {
    var response = await CreateHttpsClient().GetAsync("/test/auth/protected");

    await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Signed_in_call_reaches_protected_route()
  {
    var client = CreateHttpsClient();
    await client.PostAsync("/test/auth/sign-in", null);

    var response = await client.GetAsync("/test/auth/protected");

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  [Fact]
  public async Task Signed_in_call_without_permission_returns_403_problem_json()
  {
    var client = CreateHttpsClient();
    await client.PostAsync("/test/auth/sign-in", null);

    var response = await client.GetAsync("/test/auth/admin-only");

    await AssertProblemAsync(response, HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Sign_in_sets_httponly_secure_lax_session_cookie()
  {
    var response = await CreateHttpsClient().PostAsync("/test/auth/sign-in", null);

    var cookie = AuthCookie(response).ToLowerInvariant();
    Assert.Contains("httponly", cookie);
    Assert.Contains("secure", cookie);
    Assert.Contains("samesite=lax", cookie);
    Assert.DoesNotContain("expires=", cookie);
  }

  [Fact]
  public async Task Persistent_sign_in_cookie_expires_in_14_days()
  {
    var response = await CreateHttpsClient().PostAsync("/test/auth/sign-in?persistent=true", null);

    var expires = AuthCookie(response)
        .Split(';', StringSplitOptions.TrimEntries)
        .Single(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))
        ["expires=".Length..];

    var lifetime = DateTimeOffset.Parse(expires) - DateTimeOffset.UtcNow;
    Assert.InRange(lifetime, TimeSpan.FromDays(14) - TimeSpan.FromMinutes(1), TimeSpan.FromDays(14));
  }

  [Fact]
  public void Password_and_lockout_policy_match_the_spec()
  {
    var options = _factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;

    Assert.Equal(10, options.Password.RequiredLength);
    Assert.False(options.Password.RequireDigit);
    Assert.False(options.Password.RequireLowercase);
    Assert.False(options.Password.RequireUppercase);
    Assert.False(options.Password.RequireNonAlphanumeric);
    Assert.True(options.User.RequireUniqueEmail);
    Assert.True(options.Lockout.AllowedForNewUsers);
    Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
    Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
  }

  [Theory]
  [InlineData("Development", false)]
  [InlineData("Production", true)]
  public void Confirmed_email_requirement_follows_environment_config(
      string environment, bool expected)
  {
    var services = _factory
        .WithWebHostBuilder(builder => builder.UseEnvironment(environment))
        .Services;

    var options = services.GetRequiredService<IOptions<IdentityOptions>>().Value;
    Assert.Equal(expected, options.SignIn.RequireConfirmedEmail);
  }

  [Fact]
  public void Identity_api_token_endpoints_are_not_mapped()
  {
    // MapIdentityApi's routes; its /login and /refresh can return tokens.
    string[] identityApiRoutes = ["login", "refresh", "register", "confirmEmail", "manage/info"];

    var routes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
        .OfType<RouteEndpoint>()
        .Select(endpoint => endpoint.RoutePattern.RawText?.Trim('/'));

    Assert.DoesNotContain(routes, route =>
        identityApiRoutes.Any(r => string.Equals(route, r, StringComparison.OrdinalIgnoreCase)));
  }
}
