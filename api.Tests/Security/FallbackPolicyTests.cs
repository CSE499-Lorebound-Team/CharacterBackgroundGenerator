using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.Security;

public partial class FallbackPolicyTests : IClassFixture<ApiFactory>
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
        // The Development-only API explorer and its assets (P8-03); not
        // mapped outside Development (OpenApiDocumentTests).
        .Where(route => !route.StartsWith("scalar/"))
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

  // P8-07: the authorization matrix starts with 401 for anonymous callers.
  // Rather than trusting each controller to have that test, call every
  // API endpoint that is not [AllowAnonymous], so a new controller cannot
  // ship without it. Route values are made up; authorization runs before
  // they are looked up.
  [Fact]
  public async Task Every_protected_api_endpoint_returns_401_to_anonymous_callers()
  {
    var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
        .OfType<RouteEndpoint>()
        .Where(endpoint => endpoint.RoutePattern.RawText!.TrimStart('/').StartsWith("api/"))
        .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
        .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
            .Select(method => (Method: method, Path: Fill(endpoint.RoutePattern.RawText!))))
        .ToList();

    var client = _factory.CreateClient();
    var failures = new List<string>();
    foreach (var (method, path) in endpoints)
    {
      var request = new HttpRequestMessage(new HttpMethod(method), path);
      if (method is "POST" or "PUT" or "PATCH" or "DELETE")
      {
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
      }

      var response = await client.SendAsync(request);
      if (response.StatusCode != HttpStatusCode.Unauthorized)
      {
        failures.Add($"{method} {path}: {(int)response.StatusCode}");
      }
    }

    // Guards against the query above silently matching nothing (39 when
    // this was written).
    Assert.True(endpoints.Count >= 39, $"Only {endpoints.Count} protected endpoints were found.");
    Assert.True(failures.Count == 0, "Not 401 for anonymous callers: " + string.Join("; ", failures));
  }

  private static string Fill(string template) =>
      "/" + RouteParameter().Replace(template.TrimStart('/'), match =>
          match.Groups["guid"].Success ? Guid.NewGuid().ToString() : "x");

  [GeneratedRegex(@"\{\w+(?<guid>:guid)?\}")]
  private static partial Regex RouteParameter();
}
