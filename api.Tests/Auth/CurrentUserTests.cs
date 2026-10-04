using System.Net;
using System.Security.Claims;
using Lorebound.Api.Auth;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Http;

namespace Lorebound.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class CurrentUserTests : PostgresTestBase
{
  public CurrentUserTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  [Fact]
  public async Task Current_user_reflects_the_signed_in_account()
  {
    var (client, user) = await Factory.CreateSignedInClientAsync("gm@example.com", "Game Master");

    var response = await client.GetAsync("/test/unattributed/me");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(response, "userId", "email", "displayName");
    Assert.Equal(user.Id, body.GetProperty("userId").GetGuid());
    Assert.Equal("gm@example.com", body.GetProperty("email").GetString());
    Assert.Equal("Game Master", body.GetProperty("displayName").GetString());
  }

  [Fact]
  public void Throws_when_nobody_is_signed_in()
  {
    var anonymous = new CurrentUser(new HttpContextAccessor
    {
      HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
    });
    var noRequest = new CurrentUser(new HttpContextAccessor());

    Assert.Throws<UnauthorizedAccessException>(() => anonymous.UserId);
    Assert.Throws<UnauthorizedAccessException>(() => anonymous.Email);
    Assert.Throws<UnauthorizedAccessException>(() => noRequest.DisplayName);
  }
}
