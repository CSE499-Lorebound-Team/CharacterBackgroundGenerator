using System.Net;
using Lorebound.Api.Auth;
using Lorebound.Api.Security;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Lorebound.Api.Tests.Security;

// P8-07: the settings the security review relies on, checked on a host
// configured like the Render deployment in ADR 0003.
public class ProductionHardeningTests : IClassFixture<ApiFactory>
{
  private const string VercelOrigin = "https://lorebound.vercel.app";
  private const string ApiHost = "lorebound-api.onrender.com";

  private readonly ApiFactory _factory;

  public ProductionHardeningTests(ApiFactory factory)
  {
    _factory = factory;
  }

  private WebApplicationFactory<Program> Production(params (string Key, string Value)[] settings) =>
      _factory.WithWebHostBuilder(builder =>
      {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value)));
      });

  private static HttpClient HttpsClient(WebApplicationFactory<Program> factory) =>
      factory.CreateClient(new WebApplicationFactoryClientOptions
      {
        BaseAddress = new Uri("https://localhost"),
      });

  [Theory]
  [InlineData("/api/health")]
  [InlineData("/api/settings")]
  [InlineData("/test/errors/unhandled")]
  public async Task Production_responses_carry_HSTS_even_errors(string path)
  {
    // Plain http, as Render's proxy forwards it.
    var response = await Production().CreateClient().GetAsync(path);

    Assert.Equal(
        StrictTransportSecurityMiddleware.HeaderValue,
        Assert.Single(response.Headers.GetValues("Strict-Transport-Security")));
  }

  [Fact]
  public async Task Development_sends_no_HSTS()
  {
    var response = await _factory.CreateClient().GetAsync("/api/settings");

    Assert.False(response.Headers.Contains("Strict-Transport-Security"));
  }

  [Fact]
  public async Task Production_auth_cookie_is_secure_httponly_lax_and_host_only()
  {
    var response = await HttpsClient(Production()).PostAsync("/test/auth/sign-in", null);

    var cookie = response.Headers.GetValues("Set-Cookie")
        .Single(c => c.StartsWith(AuthenticationSetup.CookieName + "="))
        .ToLowerInvariant();
    Assert.Contains("httponly", cookie);
    Assert.Contains("secure", cookie);
    Assert.Contains("samesite=lax", cookie);
    Assert.Contains("path=/", cookie);
    // No Domain attribute: the cookie belongs to the host the browser sees
    // (the Vercel host, ADR 0001 and 0003), not its subdomains.
    Assert.DoesNotContain("domain=", cookie);
  }

  [Theory]
  [InlineData(VercelOrigin, HttpStatusCode.NoContent)]
  // Vercel preview deployments have their own origins and are not allowed
  // (ADR 0003, "Configuration").
  [InlineData("https://lorebound-git-feature-x-team.vercel.app", HttpStatusCode.Forbidden)]
  [InlineData("https://lorebound-api.onrender.com", HttpStatusCode.Forbidden)]
  [InlineData("http://lorebound.vercel.app", HttpStatusCode.Forbidden)]
  public async Task Production_writes_are_accepted_only_from_the_vercel_origin(
      string origin, HttpStatusCode expected)
  {
    var client = Production(("Cors:AllowedOrigins:0", VercelOrigin)).CreateClient();
    var request = new HttpRequestMessage(HttpMethod.Post, "/test/auth/sign-in");
    request.Headers.Add("Origin", origin);

    var response = await client.SendAsync(request);

    Assert.Equal(expected, response.StatusCode);
  }

  [Theory]
  [InlineData(ApiHost, true)]
  [InlineData("evil.example", false)]
  public async Task Allowed_hosts_refuses_other_host_headers(string host, bool allowed)
  {
    var client = Production(("AllowedHosts", ApiHost)).CreateClient();
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/settings");
    request.Headers.Host = host;

    var response = await client.SendAsync(request);

    // An allowed host reaches the API (401, nobody is signed in); the host
    // filter answers 400 before anything else runs.
    Assert.Equal(allowed ? HttpStatusCode.Unauthorized : HttpStatusCode.BadRequest, response.StatusCode);
  }
}
