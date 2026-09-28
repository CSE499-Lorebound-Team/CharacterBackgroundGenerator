using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;

namespace Lorebound.Api.Tests.Startup;

public class ConnectionStringTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;

  public ConnectionStringTests(ApiFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public void Missing_connection_string_fails_at_startup_with_guidance()
  {
    using var factory = _factory.WithWebHostBuilder(builder =>
        builder.UseSetting("ConnectionStrings:DefaultConnection", ""));

    var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

    Assert.Contains("DefaultConnection", error.Message);
    Assert.Contains("user-secrets", error.Message);
  }
}
