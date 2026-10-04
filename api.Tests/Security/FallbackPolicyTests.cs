using System.Net;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.Security;

public class FallbackPolicyTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;

  public FallbackPolicyTests(ApiFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task Controller_without_auth_attributes_returns_401_for_anonymous_calls()
  {
    var response = await _factory.CreateClient().GetAsync("/test/unattributed");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    await JsonAssert.ReadProblemAsync(response);
  }

  [Fact]
  public async Task Controller_without_auth_attributes_allows_signed_in_calls()
  {
    var client = _factory.CreateCookieClient();
    await client.PostAsync("/test/auth/sign-in", null);

    var response = await client.GetAsync("/test/unattributed");

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  [Fact]
  public async Task OpenApi_document_stays_anonymous_in_development()
  {
    var response = await _factory.CreateClient().GetAsync("/openapi/v1.json");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public void Only_the_intended_api_endpoints_are_anonymous()
  {
    var anonymous = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
        .OfType<RouteEndpoint>()
        .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        // Test-only controllers are not part of the API.
        .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()
            ?.ControllerTypeInfo.Assembly != typeof(FallbackPolicyTests).Assembly)
        .Select(endpoint => endpoint.RoutePattern.RawText!.Trim('/').ToLowerInvariant())
        .Order();

    Assert.Equal(
        [
          "api/auth/confirm-email",
          "api/auth/forgot-password",
          "api/auth/login",
          "api/auth/register",
          "api/auth/resend-confirmation",
          "api/auth/reset-password",
          "api/health",
          "openapi/{documentname}.json",
        ],
        anonymous);
  }
}
