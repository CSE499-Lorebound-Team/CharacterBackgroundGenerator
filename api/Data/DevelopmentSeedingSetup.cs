namespace Lorebound.Api.Data;

/// <summary>
/// Runs <see cref="DevelopmentSeeder"/> at startup when <c>Seed:Enabled</c>
/// is true, which <c>dotnet run -- --seed</c> sets (see Program). Seeding
/// finishes before the API starts serving. Outside Development, enabling it
/// stops startup instead, so sample users never reach Production.
/// </summary>
public static class DevelopmentSeedingSetup
{
  public const string EnabledKey = "Seed:Enabled";

  public const string CommandLineFlag = "--seed";

  public static IServiceCollection AddDevelopmentSeeding(this IServiceCollection services)
  {
    services.AddScoped<DevelopmentSeeder>();
    services.AddHostedService<SeedOnStartup>();
    return services;
  }

  // Reads the flag when the host starts rather than when services are
  // registered, so every config source (test overrides included) is seen.
  private sealed class SeedOnStartup : IHostedService
  {
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public SeedOnStartup(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
      _services = services;
      _configuration = configuration;
      _environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      if (!_configuration.GetValue<bool>(EnabledKey))
      {
        return;
      }

      if (!_environment.IsDevelopment())
      {
        throw new InvalidOperationException(
            $"{EnabledKey} is on in the {_environment.EnvironmentName} environment. " +
            "Seed data has documented passwords and runs only in Development.");
      }

      using var scope = _services.CreateScope();
      await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
  }
}
