using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Auth;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class ResetPasswordTests : PostgresTestBase
{
  private const string Email = "player@example.com";
  private const string OldPassword = CustomWebApplicationFactory.DefaultPassword;
  private const string NewPassword = "a brand new passphrase";
  private const string InvalidLinkDetail = "This reset link is invalid or has expired.";

  public ResetPasswordTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string password) =>
      client.PostAsJsonAsync("/api/auth/login", new { email = Email, password });

  private static Task<HttpResponseMessage> ForgotAsync(HttpClient client, string email = Email) =>
      client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

  private static Task<HttpResponseMessage> ResetAsync(
      HttpClient client, string code, string newPassword = NewPassword, string email = Email) =>
      client.PostAsJsonAsync("/api/auth/reset-password", new { email, code, newPassword });

  /// <summary>Requests a reset and returns the emailed link.</summary>
  private async Task<SentEmail> RequestResetLinkAsync(HttpClient client)
  {
    Assert.Equal(HttpStatusCode.NoContent, (await ForgotAsync(client)).StatusCode);
    return Assert.Single(Factory.Emails.Sent);
  }

  private static async Task AssertInvalidLinkAsync(HttpResponseMessage response)
  {
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(InvalidLinkDetail, problem.GetProperty("detail").GetString());
  }

  [Fact]
  public async Task Forgot_password_emails_a_reset_link_to_a_confirmed_account()
  {
    var user = await Factory.CreateUserAsync(Email);

    var link = await RequestResetLinkAsync(Factory.CreateCookieClient());

    Assert.Equal(EmailKind.PasswordResetLink, link.Kind);
    Assert.Equal(user.Id, link.UserId);
    Assert.Equal("http://localhost:3000/reset-password", new Uri(link.Content).GetLeftPart(UriPartial.Path));
    Assert.Equal(Email, link.Param("email"));
    Assert.True(EmailCodes.TryDecode(link.Param("code"), out _));
  }

  [Fact]
  public async Task Forgot_password_for_an_unknown_email_returns_204_and_sends_nothing()
  {
    var response = await ForgotAsync(Factory.CreateCookieClient(), "nobody@example.com");

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Empty(Factory.Emails.Sent);
  }

  [Fact]
  public async Task Forgot_password_for_an_unconfirmed_email_returns_204_and_sends_nothing()
  {
    var client = Factory.CreateCookieClient();
    await client.PostAsJsonAsync("/api/auth/register",
        new { email = Email, password = OldPassword, displayName = "Player" });
    Factory.Emails.Clear();

    var response = await ForgotAsync(client);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Empty(Factory.Emails.Sent);
  }

  [Fact]
  public async Task Reset_changes_the_password()
  {
    await Factory.CreateUserAsync(Email);
    var client = Factory.CreateCookieClient();
    var link = await RequestResetLinkAsync(client);

    var response = await ResetAsync(client, link.Param("code"));

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, OldPassword)).StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, NewPassword)).StatusCode);
  }

  [Fact]
  public async Task Reset_ends_existing_sessions()
  {
    await Factory.CreateUserAsync(Email);
    var signedIn = Factory.CreateCookieClient();
    await LoginAsync(signedIn, OldPassword);
    Assert.Equal(HttpStatusCode.NoContent, (await signedIn.GetAsync("/test/auth/protected")).StatusCode);

    var other = Factory.CreateCookieClient();
    var link = await RequestResetLinkAsync(other);
    await ResetAsync(other, link.Param("code"));

    Assert.Equal(HttpStatusCode.Unauthorized, (await signedIn.GetAsync("/test/auth/protected")).StatusCode);
  }

  [Fact]
  public async Task Reused_code_returns_400()
  {
    await Factory.CreateUserAsync(Email);
    var client = Factory.CreateCookieClient();
    var link = await RequestResetLinkAsync(client);
    await ResetAsync(client, link.Param("code"));

    var response = await ResetAsync(client, link.Param("code"), "yet another passphrase");

    await AssertInvalidLinkAsync(response);
  }

  [Fact]
  public async Task Wrong_malformed_and_unknown_email_return_the_same_400()
  {
    await Factory.CreateUserAsync(Email);
    var client = Factory.CreateCookieClient();
    var link = await RequestResetLinkAsync(client);

    await AssertInvalidLinkAsync(await ResetAsync(client, EmailCodes.Encode("not the token")));
    await AssertInvalidLinkAsync(await ResetAsync(client, "not base64 !!"));
    await AssertInvalidLinkAsync(await ResetAsync(client, link.Param("code"), email: "nobody@example.com"));

    Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, OldPassword)).StatusCode);
  }

  [Fact]
  public async Task Weak_new_password_returns_400_keyed_by_new_password()
  {
    await Factory.CreateUserAsync(Email);
    var client = Factory.CreateCookieClient();
    var link = await RequestResetLinkAsync(client);

    var response = await ResetAsync(client, link.Param("code"), newPassword: "short");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty("NewPassword", out _));
    Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, OldPassword)).StatusCode);
  }
}
