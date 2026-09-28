using System.Net;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Security;

public class CorsTests : IClassFixture<ApiFactory>
{
  private const string FrontendOrigin = "http://localhost:3000";

  private readonly HttpClient _client;

  public CorsTests(ApiFactory factory)
  {
    _client = factory.CreateClient();
  }

  private Task<HttpResponseMessage> PreflightAsync(string origin, string requestHeaders)
  {
    var request = new HttpRequestMessage(HttpMethod.Options, "/api/health");
    request.Headers.Add("Origin", origin);
    request.Headers.Add("Access-Control-Request-Method", "POST");
    request.Headers.Add("Access-Control-Request-Headers", requestHeaders);
    return _client.SendAsync(request);
  }

  private static string? Header(HttpResponseMessage response, string name) =>
      response.Headers.TryGetValues(name, out var values) ? values.Single() : null;

  [Fact]
  public async Task Preflight_from_frontend_allows_credentials_and_headers()
  {
    var response = await PreflightAsync(FrontendOrigin, "content-type,x-requested-with");

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(FrontendOrigin, Header(response, "Access-Control-Allow-Origin"));
    Assert.Equal("true", Header(response, "Access-Control-Allow-Credentials"));

    var allowedHeaders = Header(response, "Access-Control-Allow-Headers") ?? "";
    Assert.Contains("content-type", allowedHeaders, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("x-requested-with", allowedHeaders, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task Preflight_from_other_origin_is_rejected()
  {
    var response = await PreflightAsync("https://evil.example", "content-type");

    Assert.Null(Header(response, "Access-Control-Allow-Origin"));
    Assert.Null(Header(response, "Access-Control-Allow-Credentials"));
  }
}
