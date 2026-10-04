using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Auth;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class ConfirmEmailTests : PostgresTestBase
{
  private const string Email = "new@example.com";
  private const string Password = CustomWebApplicationFactory.DefaultPassword;

  public ConfirmEmailTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  /// <summary>Registers through the API and returns the emailed link.</summary>
  private async Task<SentEmail> RegisterAsync(HttpClient client)
  {
    var response = await client.PostAsJsonAsync("/api/auth/register",
        new { email = Email, password = Password, displayName = "Player" });
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return Assert.Single(Factory.Emails.Sent);
  }

  private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, object body) =>
      client.PostAsJsonAsync("/api/auth/confirm-email", body);

  private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, SentEmail link) =>
      ConfirmAsync(client, new { userId = link.Param("userId"), code = link.Param("code") });

  private Task<bool> IsConfirmedAsync() =>
      Factory.WithDbAsync(db => db.Users.Where(u => u.Email == Email)
          .Select(u => u.EmailConfirmed).SingleAsync());

  [Fact]
  public async Task Valid_code_confirms_the_email()
  {
    var client = Factory.CreateCookieClient();
    var link = await RegisterAsync(client);

    var response = await ConfirmAsync(client, link);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.True(await IsConfirmedAsync());
  }

  [Fact]
  public async Task Reused_code_returns_400()
  {
    var client = Factory.CreateCookieClient();
    var link = await RegisterAsync(client);
    await ConfirmAsync(client, link);

    var response = await ConfirmAsync(client, link);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
  }

  [Fact]
  public async Task Wrong_malformed_and_unknown_user_codes_return_the_same_400()
  {
    var client = Factory.CreateCookieClient();
    var link = await RegisterAsync(client);
    var userId = link.Param("userId");

    var wrong = await ConfirmAsync(client, new { userId, code = EmailCodes.Encode("not the token") });
    var malformed = await ConfirmAsync(client, new { userId, code = "not base64 !!" });
    var unknownUser = await ConfirmAsync(client, new { userId = Guid.NewGuid(), code = link.Param("code") });

    foreach (var response in new[] { wrong, malformed, unknownUser })
    {
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      var problem = await JsonAssert.ReadProblemAsync(response);
      Assert.Equal(
          "This confirmation link is invalid or has already been used.",
          problem.GetProperty("detail").GetString());
    }

    Assert.False(await IsConfirmedAsync());
  }

  [Fact]
  public async Task Missing_fields_return_400()
  {
    var response = await ConfirmAsync(Factory.CreateCookieClient(), new { });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Confirming_lets_the_user_sign_in_when_confirmation_is_required()
  {
    var client = Factory.WithConfig("Auth:RequireConfirmedEmail", "true").CreateCookieClient();
    var link = await RegisterAsync(client);
    var login = new { email = Email, password = Password };

    Assert.Equal(HttpStatusCode.Unauthorized,
        (await client.PostAsJsonAsync("/api/auth/login", login)).StatusCode);
    await ConfirmAsync(client, link);

    Assert.Equal(HttpStatusCode.OK,
        (await client.PostAsJsonAsync("/api/auth/login", login)).StatusCode);
  }

  [Fact]
  public async Task Resend_sends_a_new_link_that_confirms()
  {
    var client = Factory.CreateCookieClient();
    await RegisterAsync(client);
    Factory.Emails.Clear();

    var response = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email = "NEW@example.com" });

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    var link = Assert.Single(Factory.Emails.Sent);
    Assert.Equal(EmailKind.ConfirmationLink, link.Kind);
    Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(client, link)).StatusCode);
  }

  [Fact]
  public async Task Resend_for_an_unknown_email_returns_204_and_sends_nothing()
  {
    var response = await Factory.CreateCookieClient().PostAsJsonAsync(
        "/api/auth/resend-confirmation", new { email = "nobody@example.com" });

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Empty(Factory.Emails.Sent);
  }

  [Fact]
  public async Task Resend_for_a_confirmed_email_returns_204_and_sends_nothing()
  {
    await Factory.CreateUserAsync(Email);

    var response = await Factory.CreateCookieClient().PostAsJsonAsync(
        "/api/auth/resend-confirmation", new { email = Email });

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Empty(Factory.Emails.Sent);
  }
}
