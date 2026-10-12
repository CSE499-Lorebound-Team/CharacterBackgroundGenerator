using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Auth;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Tests.Auth;

// P8-08: the Data Protection keys live in Postgres, so a restarted API (a
// new host with an empty disk, as on Render's free plan) still reads the
// cookies and emailed links an earlier instance issued.
[Collection(PostgresCollection.Name)]
public class DataProtectionKeysTests : PostgresTestBase
{
  private const string Email = "player@example.com";

  public DataProtectionKeysTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  // A client that leaves cookies alone, so the test can carry the auth
  // cookie from one host to the other itself.
  private static HttpClient RawClient(WebApplicationFactory<Program> factory) =>
      factory.CreateClient(new WebApplicationFactoryClientOptions
      {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
      });

  [Fact]
  public async Task Keys_are_stored_in_the_database()
  {
    await Factory.CreateSignedInClientAsync(Email);

    var keys = await Factory.WithDbAsync(db => db.DataProtectionKeys.ToListAsync());

    Assert.NotEmpty(keys);
    Assert.All(keys, key => Assert.Contains("<key ", key.Xml));
  }

  [Fact]
  public async Task A_cookie_from_one_api_instance_authenticates_on_a_new_one()
  {
    await Factory.CreateUserAsync(Email);
    var login = await RawClient(Factory).PostAsJsonAsync(
        "/api/auth/login", new { email = Email, password = CustomWebApplicationFactory.DefaultPassword });
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    var cookie = login.Headers.GetValues("Set-Cookie")
        .Single(value => value.StartsWith(AuthenticationSetup.CookieName + "="))
        .Split(';')[0];

    // A separate host with its own services and key cache, like a restart.
    await using var restarted = new CustomWebApplicationFactory(Fixture.ConnectionString);
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
    request.Headers.Add("Cookie", cookie);
    var response = await RawClient(restarted).SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task A_reset_link_from_one_api_instance_works_on_a_new_one()
  {
    await Factory.CreateUserAsync(Email);
    var forgot = await Factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });
    Assert.Equal(HttpStatusCode.NoContent, forgot.StatusCode);
    var code = Assert.Single(Factory.Emails.Sent).Param("code");

    await using var restarted = new CustomWebApplicationFactory(Fixture.ConnectionString);
    var reset = await restarted.CreateClient().PostAsJsonAsync(
        "/api/auth/reset-password", new { email = Email, code, newPassword = "a brand new passphrase" });

    Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
  }
}
