using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class RegisterTests : PostgresTestBase
{
  private const string Password = CustomWebApplicationFactory.DefaultPassword;

  public RegisterTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> RegisterAsync(
      HttpClient client, string email, string password = Password, string displayName = "Player") =>
      client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName });

  [Fact]
  public async Task Valid_registration_returns_201_with_user_summary_only()
  {
    var response = await RegisterAsync(Factory.CreateCookieClient(), "new@example.com", displayName: "  Aria  ");

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(
        response, "id", "email", "displayName", "emailConfirmed");
    Assert.Equal("new@example.com", body.GetProperty("email").GetString());
    Assert.Equal("Aria", body.GetProperty("displayName").GetString());
    Assert.False(body.GetProperty("emailConfirmed").GetBoolean());

    var id = body.GetProperty("id").GetGuid();
    var saved = await Factory.WithDbAsync(db => db.Users.SingleAsync(u => u.Id == id));
    Assert.Equal("Aria", saved.DisplayName);
    Assert.NotEqual(default, saved.CreatedAt);
  }

  [Fact]
  public async Task Registration_does_not_sign_the_user_in()
  {
    var response = await RegisterAsync(Factory.CreateCookieClient(), "new@example.com");

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.False(response.Headers.Contains("Set-Cookie"));
  }

  [Fact]
  public async Task Email_with_characters_outside_the_username_defaults_is_accepted()
  {
    var response = await RegisterAsync(Factory.CreateCookieClient(), "o'brien+lore@example.com");

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Fact]
  public async Task Weak_password_returns_400_keyed_by_password()
  {
    var response = await RegisterAsync(Factory.CreateCookieClient(), "new@example.com", password: "short");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty("Password", out _));
  }

  [Fact]
  public async Task Duplicate_email_returns_400_keyed_by_email_when_confirmation_is_off()
  {
    await Factory.CreateUserAsync("taken@example.com");

    var response = await RegisterAsync(Factory.CreateCookieClient(), "TAKEN@example.com");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var errors = (await JsonAssert.ReadProblemAsync(response)).GetProperty("errors");
    Assert.True(errors.TryGetProperty("Email", out _));
    Assert.False(errors.TryGetProperty("UserName", out _));
  }

  [Fact]
  public async Task Duplicate_email_returns_generic_400_when_confirmation_is_required()
  {
    await Factory.CreateUserAsync("taken@example.com");
    var client = Factory.WithConfig("Auth:RequireConfirmedEmail", "true").CreateCookieClient();

    var response = await RegisterAsync(client, "taken@example.com");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await response.Content.ReadAsStringAsync();
    Assert.Contains("Could not register with these details.", body);
    Assert.DoesNotContain("taken", body, StringComparison.OrdinalIgnoreCase);
  }

  [Theory]
  [InlineData("not-an-email", "Player", "Email")]
  [InlineData("", "Player", "Email")]
  [InlineData("new@example.com", "", "DisplayName")]
  [InlineData("new@example.com", "   ", "DisplayName")]
  public async Task Invalid_fields_return_400_keyed_by_field(
      string email, string displayName, string field)
  {
    var response = await RegisterAsync(Factory.CreateCookieClient(), email, displayName: displayName);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
  }

  [Fact]
  public async Task Display_name_over_60_characters_is_rejected()
  {
    var response = await RegisterAsync(
        Factory.CreateCookieClient(), "new@example.com", displayName: new string('a', 61));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
}
