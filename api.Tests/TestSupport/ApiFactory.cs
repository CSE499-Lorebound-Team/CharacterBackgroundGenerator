using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.TestSupport;

/// <summary>
/// Hosts the real API in memory and adds the test-only controllers in this
/// assembly (routes under /test). The connection string points at a closed
/// port; these tests never touch a database (P0-09 adds a real Postgres).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
  public const string UnusedConnectionString =
      "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1";

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseSetting("ConnectionStrings:DefaultConnection", UnusedConnectionString);

    builder.ConfigureServices(services =>
        services
            .AddControllers()
            .AddApplicationPart(typeof(ApiFactory).Assembly));
  }
}
