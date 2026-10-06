using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Lorebound.Api.Tests.TestSupport;

public static class FactoryExtensions
{
  /// <summary>
  /// A client that stores and resends cookies like a browser, for cookie auth.
  /// It uses https because the auth cookie is Secure and is not resent over http.
  /// </summary>
  public static HttpClient CreateCookieClient(this WebApplicationFactory<Program> factory) =>
      factory.CreateClient(new WebApplicationFactoryClientOptions
      {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
      });

  /// <summary>
  /// A copy of the factory with one config value overridden, e.g.
  /// <c>WithConfig("Auth:RequireConfirmedEmail", "true")</c>. It is added after
  /// the appsettings files, so it wins over them.
  /// </summary>
  public static WebApplicationFactory<Program> WithConfig(
      this WebApplicationFactory<Program> factory, string key, string? value) =>
      factory.WithWebHostBuilder(builder =>
          builder.ConfigureAppConfiguration((_, config) =>
              config.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })));
}
