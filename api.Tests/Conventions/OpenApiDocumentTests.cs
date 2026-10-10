using System.Net;
using System.Text.Json;
using Lorebound.Api.Controllers;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.Conventions;

// P8-03: every endpoint appears in the OpenAPI document with a summary and
// request/response schemas, and the cross-cutting rules (cookie auth, CSRF
// header, rate limits, problem+json) are declared wherever they apply.
public class OpenApiDocumentTests : IClassFixture<ApiFactory>
{
  private static readonly string[] UnsafeMethods = ["post", "put", "patch", "delete"];

  private readonly ApiFactory _factory;

  public OpenApiDocumentTests(ApiFactory factory)
  {
    _factory = factory;
  }

  private async Task<JsonElement> DocumentAsync()
  {
    var response = await _factory.CreateClient().GetAsync("/openapi/v1.json");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static IEnumerable<(string Name, JsonElement Operation)> Operations(JsonElement document) =>
      document.GetProperty("paths").EnumerateObject()
          // Test-only controllers are not part of the API.
          .Where(path => path.Name.StartsWith("/api/"))
          .SelectMany(path => path.Value.EnumerateObject()
              .Select(op => ($"{op.Name.ToUpperInvariant()} {path.Name}", op.Value)));

  private static List<string> Missing(JsonElement document, Func<string, JsonElement, bool> ok) =>
      Operations(document).Where(op => !ok(op.Name, op.Operation)).Select(op => op.Name).ToList();

  private static bool Has(JsonElement element, string property) => element.TryGetProperty(property, out _);

  private static JsonElement Responses(JsonElement operation) => operation.GetProperty("responses");

  [Fact]
  public async Task Every_api_action_appears_in_the_document()
  {
    var document = await DocumentAsync();
    var actions = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
        .OfType<RouteEndpoint>()
        .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo.Assembly
            == typeof(HealthController).Assembly)
        .Select(e => (
            Method: e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single(),
            Path: "/" + string.Join("/", e.RoutePattern.PathSegments.Select(segment =>
                string.Concat(segment.Parts.Select(part => part switch
                {
                  Microsoft.AspNetCore.Routing.Patterns.RoutePatternParameterPart p => "{" + p.Name + "}",
                  Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart l => l.Content,
                  _ => string.Empty,
                }))))))
        .Select(a => $"{a.Method} {a.Path}")
        .Order()
        .ToList();

    Assert.Equal(actions, Operations(document).Select(op => op.Name).Order());
  }

  [Fact]
  public async Task Every_operation_has_a_summary_and_a_success_response()
  {
    var document = await DocumentAsync();

    Assert.Empty(Missing(document, (_, op) => Has(op, "summary")));
    Assert.Empty(Missing(document, (_, op) =>
        Responses(op).EnumerateObject().Any(r => r.Name.StartsWith('2'))));
  }

  [Fact]
  public async Task Bodies_have_json_schemas()
  {
    var document = await DocumentAsync();

    // Every success response except 204 has a JSON schema...
    Assert.Empty(Missing(document, (_, op) => Responses(op).EnumerateObject()
        .Where(r => r.Name.StartsWith('2') && r.Name != "204")
        .All(r => r.Value.GetProperty("content").GetProperty("application/json").TryGetProperty("schema", out JsonElement _))));
    // ...and every request body is JSON only, with a schema.
    Assert.Empty(Missing(document, (_, op) =>
        !op.TryGetProperty("requestBody", out var body)
        || (body.GetProperty("content").EnumerateObject().Select(c => c.Name).SequenceEqual(["application/json"])
            && Has(body.GetProperty("content").GetProperty("application/json"), "schema"))));
  }

  [Fact]
  public async Task Errors_are_problem_json()
  {
    var document = await DocumentAsync();

    Assert.Empty(Missing(document, (name, op) => Responses(op).EnumerateObject()
        .Where(r => int.Parse(r.Name) >= 400 && name != "GET /api/Health")
        .All(r => r.Value.GetProperty("content").EnumerateObject().Select(c => c.Name)
            .SequenceEqual(["application/problem+json"]))));
    var badRequest = Responses(document.GetProperty("paths").GetProperty("/api/settings").GetProperty("post"))
        .GetProperty("400").GetProperty("content").GetProperty("application/problem+json").GetProperty("schema");
    Assert.True(badRequest.GetProperty("properties").TryGetProperty("errors", out JsonElement _));
  }

  [Fact]
  public async Task Signed_in_endpoints_declare_the_cookie_and_401()
  {
    var document = await DocumentAsync();
    var anonymous = Operations(document).Where(op => !Has(op.Operation, "security")).Select(op => op.Name).Order();

    Assert.Equal(
        [
          "GET /api/Health",
          "POST /api/auth/confirm-email",
          "POST /api/auth/forgot-password",
          "POST /api/auth/login",
          "POST /api/auth/register",
          "POST /api/auth/resend-confirmation",
          "POST /api/auth/reset-password",
        ],
        anonymous);
    Assert.Empty(Missing(document, (_, op) => !Has(op, "security") || Has(Responses(op), "401")));
    var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("cookieAuth");
    Assert.Equal("cookie", scheme.GetProperty("in").GetString());
    Assert.Equal("lorebound.auth", scheme.GetProperty("name").GetString());
  }

  [Fact]
  public async Task Writes_declare_the_csrf_header_and_403()
  {
    var document = await DocumentAsync();

    Assert.Empty(Missing(document, (name, op) =>
        !UnsafeMethods.Contains(name.Split(' ')[0].ToLowerInvariant())
        || (op.GetProperty("parameters").EnumerateArray().Any(p =>
                p.GetProperty("name").GetString() == "X-Requested-With"
                && p.GetProperty("in").GetString() == "header"
                && p.GetProperty("required").GetBoolean())
            && Has(Responses(op), "403"))));
    // Reads need no CSRF header.
    Assert.Empty(Missing(document, (name, op) =>
        !name.StartsWith("GET ")
        || !op.TryGetProperty("parameters", out var parameters)
        || parameters.EnumerateArray().All(p => p.GetProperty("name").GetString() != "X-Requested-With")));
  }

  [Fact]
  public async Task Rate_limited_endpoints_declare_429()
  {
    var document = await DocumentAsync();
    var limited = Operations(document).Where(op => Has(Responses(op.Operation), "429")).Select(op => op.Name).Order();

    Assert.Equal(
        [
          "DELETE /api/users/me",
          "GET /api/invites/{code}",
          "POST /api/auth/forgot-password",
          "POST /api/auth/login",
          "POST /api/auth/register",
          "POST /api/auth/resend-confirmation",
          "POST /api/invites/{code}/accept",
        ],
        limited);
  }

  [Fact]
  public async Task The_description_documents_cookie_auth_and_the_csrf_header()
  {
    var info = (await DocumentAsync()).GetProperty("info");

    Assert.Equal("Lorebound API", info.GetProperty("title").GetString());
    var description = info.GetProperty("description").GetString()!;
    Assert.Contains("lorebound.auth", description);
    Assert.Contains("X-Requested-With: Lorebound", description);
    Assert.Contains("traceId", description);
  }

  [Fact]
  public async Task The_explorer_is_served_in_Development()
  {
    var response = await _factory.CreateClient().GetAsync("/scalar/v1");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Neither_the_document_nor_the_explorer_is_served_outside_Development()
  {
    using var production = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
    var client = production.CreateClient();

    foreach (var url in new[] { "/openapi/v1.json", "/scalar/v1" })
    {
      var response = await client.GetAsync(url);
      Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
  }
}
