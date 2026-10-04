using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;

namespace Lorebound.Api.Tests.Errors;

public class ProblemDetailsTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;

  public ProblemDetailsTests(ApiFactory factory)
  {
    _factory = factory;
  }

  private static async Task<JsonElement> ReadProblemAsync(
      HttpResponseMessage response)
  {
    Assert.Equal(
        "application/problem+json",
        response.Content.Headers.ContentType?.MediaType);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var problem = json.RootElement.Clone();

    Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    Assert.Equal((int)response.StatusCode, problem.GetProperty("status").GetInt32());

    return problem;
  }

  [Theory]
  [InlineData("not-found", HttpStatusCode.NotFound)]
  [InlineData("forbidden", HttpStatusCode.Forbidden)]
  [InlineData("conflict", HttpStatusCode.Conflict)]
  public async Task Domain_exceptions_map_to_problem_responses(
      string route, HttpStatusCode expected)
  {
    var response = await _factory.CreateClient().GetAsync($"/test/errors/{route}");

    Assert.Equal(expected, response.StatusCode);
    await ReadProblemAsync(response);
  }

  [Fact]
  public async Task NotFoundException_message_is_the_detail()
  {
    var response = await _factory.CreateClient().GetAsync("/test/errors/not-found");

    var problem = await ReadProblemAsync(response);
    Assert.Equal("Setting not found.", problem.GetProperty("detail").GetString());
  }

  [Fact]
  public async Task Invalid_body_returns_validation_problem_with_field_errors()
  {
    var response = await _factory.CreateClient().PostAsJsonAsync(
        "/test/errors/validate", new { name = "far too long a name" });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty("Name", out _));
  }

  [Fact]
  public async Task Unmatched_route_returns_404_problem_json_when_signed_in()
  {
    var client = _factory.CreateCookieClient();
    await client.PostAsync("/test/auth/sign-in", null);

    var response = await client.GetAsync("/api/does-not-exist");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    await ReadProblemAsync(response);
  }

  [Fact]
  public async Task Unmatched_route_returns_401_problem_json_when_anonymous()
  {
    // The fallback policy also covers requests that match no endpoint.
    var response = await _factory.CreateClient().GetAsync("/api/does-not-exist");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    await ReadProblemAsync(response);
  }

  [Fact]
  public async Task Unhandled_exception_outside_development_hides_details()
  {
    var client = _factory
        .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
        .CreateClient();

    var response = await client.GetAsync("/test/errors/unhandled");

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    await ReadProblemAsync(response);

    var body = await response.Content.ReadAsStringAsync();
    Assert.DoesNotContain("secret internal detail", body);
    Assert.DoesNotContain("InvalidOperationException", body);
  }
}
