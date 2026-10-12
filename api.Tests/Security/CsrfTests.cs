using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Security;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Security;

public class CsrfTests : IClassFixture<ApiFactory>
{
  // Anonymous test route: 204 when the request gets through.
  private const string Route = "/test/errors/validate";
  private const string FrontendOrigin = "http://localhost:3000";

  private readonly ApiFactory _factory;

  public CsrfTests(ApiFactory factory)
  {
    _factory = factory;
  }

  private HttpClient ClientWithoutCsrfHeader()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Remove(CsrfProtectionMiddleware.HeaderName);
    return client;
  }

  private static HttpRequestMessage Post(string? origin = null)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, Route)
    {
      Content = JsonContent.Create(new { name = "ok" }),
    };
    if (origin is not null)
    {
      request.Headers.Add("Origin", origin);
    }

    return request;
  }

  private static async Task AssertForbiddenAsync(HttpResponseMessage response, string detail)
  {
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.Equal(detail, problem.GetProperty("detail").GetString());
  }

  [Fact]
  public async Task Post_without_the_header_returns_403()
  {
    var response = await ClientWithoutCsrfHeader().SendAsync(Post());

    await AssertForbiddenAsync(response, "Missing or invalid X-Requested-With header.");
  }

  [Fact]
  public async Task Post_with_the_wrong_header_value_returns_403()
  {
    var client = ClientWithoutCsrfHeader();
    client.DefaultRequestHeaders.Add(CsrfProtectionMiddleware.HeaderName, "XMLHttpRequest");

    var response = await client.SendAsync(Post());

    await AssertForbiddenAsync(response, "Missing or invalid X-Requested-With header.");
  }

  [Theory]
  [InlineData("PUT")]
  [InlineData("PATCH")]
  [InlineData("DELETE")]
  public async Task Other_unsafe_methods_without_the_header_return_403(string method)
  {
    var response = await ClientWithoutCsrfHeader().SendAsync(
        new HttpRequestMessage(new HttpMethod(method), Route));

    await AssertForbiddenAsync(response, "Missing or invalid X-Requested-With header.");
  }

  [Fact]
  public async Task Login_without_the_header_is_refused_before_signing_in()
  {
    var response = await ClientWithoutCsrfHeader().PostAsJsonAsync(
        "/api/auth/login", new { email = "player@example.com", password = "whatever" });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.False(response.Headers.Contains("Set-Cookie"));
  }

  [Fact]
  public async Task Header_with_a_disallowed_origin_returns_403()
  {
    var response = await _factory.CreateClient().SendAsync(Post(origin: "https://evil.example"));

    await AssertForbiddenAsync(response, "Origin not allowed.");
  }

  [Theory]
  [InlineData(FrontendOrigin)]
  [InlineData("HTTP://LOCALHOST:3000/")]
  public async Task Header_with_an_allowed_origin_passes(string origin)
  {
    var response = await _factory.CreateClient().SendAsync(Post(origin));

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  [Fact]
  public async Task Header_without_an_origin_passes()
  {
    var response = await _factory.CreateClient().SendAsync(Post());

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  [Fact]
  public async Task Safe_methods_need_no_header()
  {
    var response = await ClientWithoutCsrfHeader().GetAsync("/test/errors/not-found");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Preflight_needs_no_header()
  {
    var request = new HttpRequestMessage(HttpMethod.Options, Route);
    request.Headers.Add("Origin", FrontendOrigin);
    request.Headers.Add("Access-Control-Request-Method", "POST");
    request.Headers.Add("Access-Control-Request-Headers", "content-type,x-requested-with");

    var response = await ClientWithoutCsrfHeader().SendAsync(request);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }
}
