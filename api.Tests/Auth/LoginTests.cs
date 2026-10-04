using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Auth;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class LoginTests : PostgresTestBase
{
  private const string Email = "player@example.com";
  private const string Password = CustomWebApplicationFactory.DefaultPassword;

  public LoginTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> LoginAsync(
      HttpClient client, string email = Email, string password = Password, bool rememberMe = false) =>
      client.PostAsJsonAsync("/api/auth/login", new { email, password, rememberMe });

  private static string AuthCookie(HttpResponseMessage response) =>
      response.Headers.GetValues("Set-Cookie")
          .Single(c => c.StartsWith(AuthenticationSetup.CookieName + "="));

  private static async Task<(string? Title, string? Detail)> ProblemTextAsync(
      HttpResponseMessage response)
  {
    var problem = await JsonAssert.ReadProblemAsync(response);
    return (problem.GetProperty("title").GetString(), problem.GetProperty("detail").GetString());
  }

  [Fact]
  public async Task Valid_login_returns_user_summary_and_session_cookie()
  {
    var user = await Factory.CreateUserAsync(Email, "Game Master");

    var response = await LoginAsync(Factory.CreateCookieClient());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(response, "id", "email", "displayName");
    Assert.Equal(user.Id, body.GetProperty("id").GetGuid());
    Assert.Equal("Game Master", body.GetProperty("displayName").GetString());

    var cookie = AuthCookie(response).ToLowerInvariant();
    Assert.Contains("httponly", cookie);
    Assert.Contains("secure", cookie);
    Assert.Contains("samesite=lax", cookie);
    Assert.DoesNotContain("expires=", cookie);
  }

  [Fact]
  public async Task Login_cookie_authenticates_later_requests()
  {
    await Factory.CreateUserAsync(Email);
    var client = Factory.CreateCookieClient();

    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/test/auth/protected")).StatusCode);
    await LoginAsync(client);

    Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/test/auth/protected")).StatusCode);
  }

  [Fact]
  public async Task Remember_me_issues_a_14_day_persistent_cookie()
  {
    await Factory.CreateUserAsync(Email);

    var response = await LoginAsync(Factory.CreateCookieClient(), rememberMe: true);

    var expires = AuthCookie(response)
        .Split(';', StringSplitOptions.TrimEntries)
        .Single(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))
        ["expires=".Length..];
    var lifetime = DateTimeOffset.Parse(expires) - DateTimeOffset.UtcNow;
    Assert.InRange(lifetime, TimeSpan.FromDays(14) - TimeSpan.FromMinutes(1), TimeSpan.FromDays(14));
  }

  [Fact]
  public async Task Email_lookup_ignores_case_and_surrounding_spaces()
  {
    await Factory.CreateUserAsync(Email);

    var response = await LoginAsync(Factory.CreateCookieClient(), email: "  PLAYER@Example.com ");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Wrong_password_and_unknown_email_get_the_same_401()
  {
    await Factory.CreateUserAsync(Email);
    var client = Factory.CreateCookieClient();

    var wrongPassword = await LoginAsync(client, password: "not the password");
    var unknownEmail = await LoginAsync(client, email: "nobody@example.com");

    Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
    Assert.Equal(await ProblemTextAsync(wrongPassword), await ProblemTextAsync(unknownEmail));
    Assert.False(wrongPassword.Headers.Contains("Set-Cookie"));
  }

  [Fact]
  public async Task Unconfirmed_email_gets_the_generic_401_when_confirmation_is_required()
  {
    var client = Factory.WithConfig("Auth:RequireConfirmedEmail", "true").CreateCookieClient();
    await client.PostAsJsonAsync("/api/auth/register",
        new { email = Email, password = Password, displayName = "Player" });

    var unconfirmed = await LoginAsync(client);
    var unknownEmail = await LoginAsync(client, email: "nobody@example.com");

    Assert.Equal(HttpStatusCode.Unauthorized, unconfirmed.StatusCode);
    Assert.Equal(await ProblemTextAsync(unknownEmail), await ProblemTextAsync(unconfirmed));
  }

  [Fact]
  public async Task Five_failures_lock_the_account_even_for_the_right_password()
  {
    await Factory.CreateUserAsync(Email);
    var client = Factory.CreateCookieClient();

    for (var attempt = 0; attempt < 5; attempt++)
    {
      await LoginAsync(client, password: "not the password");
    }

    var response = await LoginAsync(client);

    Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
    Assert.False(response.Headers.Contains("Set-Cookie"));

    var retryAfter = int.Parse(response.Headers.GetValues("Retry-After").Single());
    Assert.InRange(retryAfter, 14 * 60, 15 * 60);

    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(423, problem.GetProperty("status").GetInt32());
    Assert.Equal(retryAfter, problem.GetProperty("retryAfterSeconds").GetInt32());
    Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
  }

  [Fact]
  public async Task Four_failures_then_success_does_not_lock()
  {
    await Factory.CreateUserAsync(Email);
    var client = Factory.CreateCookieClient();

    for (var attempt = 0; attempt < 4; attempt++)
    {
      await LoginAsync(client, password: "not the password");
    }

    Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client)).StatusCode);
  }

  [Theory]
  [InlineData("", Password)]
  [InlineData(Email, "")]
  public async Task Missing_fields_return_400(string email, string password)
  {
    var response = await LoginAsync(Factory.CreateCookieClient(), email, password);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
}
