using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lorebound.Api.Auth;
using Lorebound.Api.Security;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lorebound.Api.Tests.Auth;

/// <summary>
/// End-to-end checks across the whole auth surface (P1-13). Per-endpoint
/// cases live in the other Auth and Security test classes; the coverage map
/// is in the README under "Auth test suite".
/// </summary>
[Collection(PostgresCollection.Name)]
public class AuthJourneyTests : PostgresTestBase
{
  private const string Email = "journey@example.com";
  private const string Password = CustomWebApplicationFactory.DefaultPassword;
  private const string FrontendOrigin = "http://localhost:3000";

  private static readonly string[] TokenWords = ["token", "access", "refresh"];

  public AuthJourneyTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  /// <summary>A cookie client that also sends Origin, like the browser does.</summary>
  private static HttpClient BrowserClient(WebApplicationFactory<Program> factory)
  {
    var client = factory.CreateCookieClient();
    client.DefaultRequestHeaders.Add("Origin", FrontendOrigin);
    return client;
  }

  private static string AuthCookie(HttpResponseMessage response) =>
      Assert.Single(response.Headers.GetValues("Set-Cookie"));

  private static IEnumerable<string> PropertyNames(JsonElement element) => element.ValueKind switch
  {
    JsonValueKind.Object => element.EnumerateObject()
        .SelectMany(property => PropertyNames(property.Value).Prepend(property.Name)),
    JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
    _ => [],
  };

  /// <summary>
  /// Fails if a JSON body has a token-like property anywhere, or contains
  /// the auth cookie's value.
  /// </summary>
  private static async Task AssertNoTokenAsync(HttpResponseMessage response, string? cookieValue = null)
  {
    var body = await response.Content.ReadAsStringAsync();
    if (cookieValue is not null)
    {
      Assert.DoesNotContain(cookieValue, body);
    }

    if (body.Length == 0)
    {
      return;
    }

    using var json = JsonDocument.Parse(body);
    var leaked = PropertyNames(json.RootElement)
        .Where(name => TokenWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));
    Assert.Empty(leaked);
  }

  [Fact]
  public async Task Register_confirm_login_profile_logout_journey()
  {
    var client = BrowserClient(Factory.WithConfig("Auth:RequireConfirmedEmail", "true"));
    var login = new { email = Email, password = Password, rememberMe = false };

    var register = await client.PostAsJsonAsync("/api/auth/register",
        new { email = Email, password = Password, displayName = "Journey" });
    Assert.Equal(HttpStatusCode.Created, register.StatusCode);
    Assert.False(register.Headers.Contains("Set-Cookie"));

    Assert.Equal(HttpStatusCode.Unauthorized,
        (await client.PostAsJsonAsync("/api/auth/login", login)).StatusCode);

    var link = Assert.Single(Factory.Emails.Sent);
    var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email",
        new { userId = link.Param("userId"), code = link.Param("code") });
    Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

    var signIn = await client.PostAsJsonAsync("/api/auth/login", login);
    Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    var cookie = AuthCookie(signIn).ToLowerInvariant();
    Assert.StartsWith(AuthenticationSetup.CookieName + "=", cookie);
    Assert.Contains("httponly", cookie);
    Assert.Contains("secure", cookie);
    Assert.Contains("samesite=lax", cookie);

    var me = await client.GetAsync("/api/users/me");
    Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    var profile = await JsonAssert.ReadJsonAsync(me);
    Assert.Equal("Journey", profile.GetProperty("displayName").GetString());
    Assert.True(profile.GetProperty("emailConfirmed").GetBoolean());

    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
  }

  [Fact]
  public async Task No_auth_response_carries_a_token()
  {
    var client = Factory.CreateCookieClient();

    var register = await client.PostAsJsonAsync("/api/auth/register",
        new { email = Email, password = Password, displayName = "Journey" });
    var confirmLink = Assert.Single(Factory.Emails.Sent);
    var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email",
        new { userId = confirmLink.Param("userId"), code = confirmLink.Param("code") });
    var resend = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email = Email });

    var login = await client.PostAsJsonAsync("/api/auth/login",
        new { email = Email, password = Password, rememberMe = true });
    var cookieValue = AuthCookie(login).Split(';')[0][(AuthenticationSetup.CookieName.Length + 1)..];
    var me = await client.GetAsync("/api/users/me");
    var update = await client.PutAsJsonAsync("/api/users/me", new { displayName = "Renamed" });
    var logout = await client.PostAsync("/api/auth/logout", null);

    Factory.Emails.Clear();
    var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });
    var resetLink = Assert.Single(Factory.Emails.Sent);
    var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
        new { email = Email, code = resetLink.Param("code"), newPassword = "a brand new passphrase" });
    var failedLogin = await client.PostAsJsonAsync("/api/auth/login",
        new { email = Email, password = Password });

    HttpResponseMessage[] responses =
        [register, confirm, resend, login, me, update, logout, forgot, reset, failedLogin];
    Assert.All(responses, response => Assert.True((int)response.StatusCode < 500));
    foreach (var response in responses)
    {
      await AssertNoTokenAsync(response, cookieValue);
    }
  }

  [Fact]
  public async Task Login_sets_only_the_auth_cookie()
  {
    await Factory.CreateUserAsync(Email);

    var response = await Factory.CreateCookieClient().PostAsJsonAsync(
        "/api/auth/login", new { email = Email, password = Password });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.StartsWith(AuthenticationSetup.CookieName + "=", AuthCookie(response));
  }

  [Fact]
  public async Task Tampered_cookie_is_rejected()
  {
    await Factory.CreateUserAsync(Email);
    var login = await Factory.CreateCookieClient().PostAsJsonAsync(
        "/api/auth/login", new { email = Email, password = Password });
    var value = AuthCookie(login).Split(';')[0][(AuthenticationSetup.CookieName.Length + 1)..];

    // Flip one character in the middle of the protected payload.
    var middle = value.Length / 2;
    var tampered = value[..middle] + (value[middle] == 'A' ? 'B' : 'A') + value[(middle + 1)..];

    Assert.Equal(HttpStatusCode.OK, (await GetMeWithCookieAsync(value)).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeWithCookieAsync(tampered)).StatusCode);
  }

  private async Task<HttpResponseMessage> GetMeWithCookieAsync(string cookieValue)
  {
    var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      BaseAddress = new Uri("https://localhost"),
      HandleCookies = false,
    });
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
    request.Headers.Add("Cookie", $"{AuthenticationSetup.CookieName}={cookieValue}");
    return await client.SendAsync(request);
  }

  [Fact]
  public async Task Lockout_affects_only_the_attacked_account()
  {
    await Factory.CreateUserAsync(Email);
    await Factory.CreateUserAsync("bystander@example.com");
    var client = Factory.CreateCookieClient();

    for (var attempt = 0; attempt < 5; attempt++)
    {
      await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = "not the password" });
    }

    Assert.Equal(HttpStatusCode.Locked, (await client.PostAsJsonAsync(
        "/api/auth/login", new { email = Email, password = Password })).StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
        "/api/auth/login", new { email = "bystander@example.com", password = Password })).StatusCode);
  }

  [Fact]
  public async Task Unknown_email_never_reports_a_lockout()
  {
    var client = Factory.CreateCookieClient();

    for (var attempt = 0; attempt < 7; attempt++)
    {
      var response = await client.PostAsJsonAsync(
          "/api/auth/login", new { email = "nobody@example.com", password = "not the password" });
      Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
  }

  [Fact]
  public async Task Browser_style_requests_without_the_csrf_header_cannot_sign_in()
  {
    await Factory.CreateUserAsync(Email);
    var client = BrowserClient(Factory);
    client.DefaultRequestHeaders.Remove(CsrfProtectionMiddleware.HeaderName);

    var response = await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.False(response.Headers.Contains("Set-Cookie"));
  }
}
