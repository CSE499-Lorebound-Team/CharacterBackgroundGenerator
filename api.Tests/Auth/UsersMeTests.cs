using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class UsersMeTests : PostgresTestBase
{
  public UsersMeTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> UpdateAsync(HttpClient client, string? displayName) =>
      client.PutAsJsonAsync("/api/users/me", new { displayName });

  [Fact]
  public async Task Anonymous_calls_return_401()
  {
    var client = Factory.CreateCookieClient();

    var get = await client.GetAsync("/api/users/me");
    var put = await UpdateAsync(client, "Someone");

    Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    await JsonAssert.ReadProblemAsync(get);
  }

  [Fact]
  public async Task Get_returns_the_signed_in_profile()
  {
    var (client, user) = await Factory.CreateSignedInClientAsync("gm@example.com", "Game Master");

    var response = await client.GetAsync("/api/users/me");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(
        response, "id", "email", "displayName", "emailConfirmed", "createdAt");
    Assert.Equal(user.Id, body.GetProperty("id").GetGuid());
    Assert.Equal("gm@example.com", body.GetProperty("email").GetString());
    Assert.Equal("Game Master", body.GetProperty("displayName").GetString());
    Assert.True(body.GetProperty("emailConfirmed").GetBoolean());
    Assert.NotEqual(default, body.GetProperty("createdAt").GetDateTimeOffset());
  }

  [Fact]
  public async Task Each_user_sees_only_their_own_profile()
  {
    var (first, firstUser) = await Factory.CreateSignedInClientAsync("one@example.com", "One");
    var (second, secondUser) = await Factory.CreateSignedInClientAsync("two@example.com", "Two");

    var firstBody = await JsonAssert.ReadJsonAsync(await first.GetAsync("/api/users/me"));
    var secondBody = await JsonAssert.ReadJsonAsync(await second.GetAsync("/api/users/me"));

    Assert.Equal(firstUser.Id, firstBody.GetProperty("id").GetGuid());
    Assert.Equal(secondUser.Id, secondBody.GetProperty("id").GetGuid());
  }

  [Fact]
  public async Task Put_updates_and_persists_the_trimmed_display_name()
  {
    var (client, user) = await Factory.CreateSignedInClientAsync();

    var response = await UpdateAsync(client, "  Renamed Player  ");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    Assert.Equal("Renamed Player", body.GetProperty("displayName").GetString());

    var saved = await Factory.WithDbAsync(db => db.Users.SingleAsync(u => u.Id == user.Id));
    Assert.Equal("Renamed Player", saved.DisplayName);

    var again = await JsonAssert.ReadJsonAsync(await client.GetAsync("/api/users/me"));
    Assert.Equal("Renamed Player", again.GetProperty("displayName").GetString());
  }

  [Fact]
  public async Task Put_keeps_the_session_and_refreshes_the_display_name_claim()
  {
    var (client, _) = await Factory.CreateSignedInClientAsync();

    await UpdateAsync(client, "Renamed Player");

    var current = await client.GetAsync("/test/unattributed/me");
    Assert.Equal(HttpStatusCode.OK, current.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(current);
    Assert.Equal("Renamed Player", body.GetProperty("displayName").GetString());
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
  public async Task Invalid_display_name_returns_400(string? displayName)
  {
    var (client, user) = await Factory.CreateSignedInClientAsync();

    var response = await UpdateAsync(client, displayName);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty("DisplayName", out _));

    var saved = await Factory.WithDbAsync(db => db.Users.SingleAsync(u => u.Id == user.Id));
    Assert.Equal("Player", saved.DisplayName);
  }
}
