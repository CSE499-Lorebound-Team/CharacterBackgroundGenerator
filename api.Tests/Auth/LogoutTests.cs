using System.Net;
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

  private async Task<HttpClient> SignedInClientAsync() =>
      (await Factory.CreateSignedInClientAsync(Email)).Client;

  [Fact]
  public async Task Logout_returns_204_and_ends_the_session()
  {
    var client = await SignedInClientAsync();
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

    var response = await client.PostAsync("/api/auth/logout", null);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
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
