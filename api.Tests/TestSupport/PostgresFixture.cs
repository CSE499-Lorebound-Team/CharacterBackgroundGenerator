using Lorebound.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Lorebound.Api.Tests.TestSupport;

/// <summary>
/// One real Postgres for the whole test run, migrated once and wiped between
/// tests with <see cref="ResetAsync"/>. Starts a Docker container unless
/// TEST_DB_CONNECTION points at an existing (throwaway) database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
  public const string ConnectionEnvironmentVariable = "TEST_DB_CONNECTION";

  private PostgreSqlContainer? _container;

  private Respawner _respawner = null!;

  public string ConnectionString { get; private set; } = string.Empty;

  public CustomWebApplicationFactory Factory { get; private set; } = null!;

  public async Task InitializeAsync()
  {
    var external = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);

    if (string.IsNullOrWhiteSpace(external))
    {
      _container = new PostgreSqlBuilder("postgres:17").Build();
      await _container.StartAsync();
      ConnectionString = _container.GetConnectionString();
    }
    else
    {
      ConnectionString = external;
      GuardAgainstNonTestDatabase(ConnectionString);
    }

    Factory = new CustomWebApplicationFactory(ConnectionString);

    using (var scope = Factory.Services.CreateScope())
    {
      var db = scope.ServiceProvider.GetRequiredService<LoreboundDbContext>();
      await db.Database.MigrateAsync();
    }

    await using var connection = new NpgsqlConnection(ConnectionString);
    await connection.OpenAsync();
    _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
    {
      DbAdapter = DbAdapter.Postgres,
      SchemasToInclude = ["public"],
      TablesToIgnore = ["__EFMigrationsHistory"],
    });
  }

  /// <summary>Deletes all rows (keeps the schema). Call at the start of each test.</summary>
  public async Task ResetAsync()
  {
    await using var connection = new NpgsqlConnection(ConnectionString);
    await connection.OpenAsync();
    await _respawner.ResetAsync(connection);
  }

  public async Task DisposeAsync()
  {
    await Factory.DisposeAsync();

    if (_container is not null)
    {
      await _container.DisposeAsync();
    }
  }

  // ResetAsync deletes every row, so never let a stray TEST_DB_CONNECTION
  // point at a development database.
  private static void GuardAgainstNonTestDatabase(string connectionString)
  {
    var database = new NpgsqlConnectionStringBuilder(connectionString).Database ?? "";

    if (!database.Contains("test", StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException(
          $"{ConnectionEnvironmentVariable} points at database '{database}'. Tests " +
          "delete all its data, so its name must contain 'test' (e.g. lorebound_test).");
    }
  }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
  public const string Name = "Postgres";
}
