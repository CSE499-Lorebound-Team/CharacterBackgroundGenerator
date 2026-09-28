using System.Net;
using System.Text.Json;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class HealthSmokeTests : PostgresTestBase
{
  public HealthSmokeTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  [Fact]
  public async Task Health_returns_200_with_database_connected()
  {
    var response = await Factory.CreateCookieClient().GetAsync("/api/health");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.Equal("connected", json.RootElement.GetProperty("database").GetString());
  }
}
