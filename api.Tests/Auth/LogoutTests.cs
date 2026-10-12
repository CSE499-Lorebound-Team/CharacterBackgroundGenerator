using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Auth;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

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
  public async Task A_copy_of_the_cookie_stops_working_after_logout()
  {
    var (client, _) = await Factory.CreateSignedInClientAsync(Email);
    var login = await client.PostAsJsonAsync("/api/auth/login",
        new { email = Email, password = CustomWebApplicationFactory.DefaultPassword });
    var copied = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    Assert.Equal(HttpStatusCode.OK, (await GetMeWithCookieAsync(copied)).StatusCode);

    await client.PostAsync("/api/auth/logout", null);

    Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeWithCookieAsync(copied)).StatusCode);
  }

  [Fact]
  public async Task Logout_signs_the_user_out_on_other_devices()
  {
    var (laptop, _) = await Factory.CreateSignedInClientAsync(Email);
    var phone = Factory.CreateCookieClient();
    await phone.PostAsJsonAsync("/api/auth/login",
        new { email = Email, password = CustomWebApplicationFactory.DefaultPassword });

    await laptop.PostAsync("/api/auth/logout", null);

    Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/users/me")).StatusCode);
  }

  /// <summary>Sends a raw Cookie header, as an attacker replaying it would.</summary>
  private async Task<HttpResponseMessage> GetMeWithCookieAsync(string cookie)
  {
    var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      BaseAddress = new Uri("https://localhost"),
      HandleCookies = false,
    });
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
    request.Headers.Add("Cookie", cookie);
    return await client.SendAsync(request);
  }

  [Fact]
  public async Task Anonymous_logout_returns_401_problem_json()
  {
    var response = await Factory.CreateCookieClient().PostAsync("/api/auth/logout", null);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
  }
}
