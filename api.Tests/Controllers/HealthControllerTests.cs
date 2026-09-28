using System.Net;
using System.Text.Json;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Controllers;

public class HealthControllerTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;

  public HealthControllerTests(ApiFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task Unreachable_database_returns_503_unhealthy()
  {
    var response = await _factory.CreateClient().GetAsync("/api/health");

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.Equal("unhealthy", json.RootElement.GetProperty("status").GetString());
    Assert.Equal("unreachable", json.RootElement.GetProperty("database").GetString());
  }
}
