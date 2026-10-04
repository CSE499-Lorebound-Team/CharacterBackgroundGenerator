using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Auth;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class LogoutTests : PostgresTestBase
{
  private const string Email = "player@example.com";

  public LogoutTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private async Task<HttpClient> SignedInClientAsync()
  {
    await Factory.CreateUserAsync(Email);
    var client = Factory.CreateCookieClient();
    var login = await client.PostAsJsonAsync("/api/auth/login",
        new { email = Email, password = CustomWebApplicationFactory.DefaultPassword });
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    return client;
  }

  [Fact]
  public async Task Logout_returns_204_and_ends_the_session()
  {
    var client = await SignedInClientAsync();
    Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/test/auth/protected")).StatusCode);

    var response = await client.PostAsync("/api/auth/logout", null);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/test/auth/protected")).StatusCode);
  }

  [Fact]
  public async Task Logout_expires_the_cookie_with_the_issued_path()
  {
    var client = await SignedInClientAsync();

    var response = await client.PostAsync("/api/auth/logout", null);

    var cookie = response.Headers.GetValues("Set-Cookie")
        .Single(c => c.StartsWith(AuthenticationSetup.CookieName + "="))
        .ToLowerInvariant();
    Assert.StartsWith(AuthenticationSetup.CookieName + "=;", cookie);
    Assert.Contains("expires=thu, 01 jan 1970", cookie);
    Assert.Contains("path=/", cookie);
  }

  [Fact]
  public async Task Anonymous_logout_returns_401_problem_json()
  {
    var response = await Factory.CreateCookieClient().PostAsync("/api/auth/logout", null);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
  }
}
