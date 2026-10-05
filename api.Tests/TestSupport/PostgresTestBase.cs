namespace Lorebound.Api.Tests.TestSupport;

/// <summary>
/// Base for tests that need the real database. Each test starts with empty
/// tables and no recorded emails (<see cref="CustomWebApplicationFactory.Emails"/>).
/// Subclasses must also be marked
/// <c>[Collection(PostgresCollection.Name)]</c> so they share one container.
/// </summary>
public abstract class PostgresTestBase : IAsyncLifetime
{
  protected PostgresTestBase(PostgresFixture fixture)
  {
    Fixture = fixture;
  }

  protected PostgresFixture Fixture { get; }

  protected CustomWebApplicationFactory Factory => Fixture.Factory;

  public Task InitializeAsync()
  {
    Factory.Emails.Clear();
    return Fixture.ResetAsync();
  }

  public Task DisposeAsync() => Task.CompletedTask;
}
