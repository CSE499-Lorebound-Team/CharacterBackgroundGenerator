using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lorebound.Api.Tests.TestSupport;

/// <summary>
/// Records the SQL every DbContext in the API runs, so tests can count the
/// queries an endpoint makes (P8-04: no N+1, a handful per request). Shared
/// by a factory and its copies; cleared before every Postgres test.
/// </summary>
public sealed class SqlCapture : DbCommandInterceptor
{
  private readonly ConcurrentQueue<string> _commands = new();

  public IReadOnlyList<string> Commands => _commands.ToList();

  public void Clear() => _commands.Clear();

  public override InterceptionResult<DbDataReader> ReaderExecuting(
      DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
  {
    _commands.Enqueue(command.CommandText);
    return result;
  }

  public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
      DbCommand command,
      CommandEventData eventData,
      InterceptionResult<DbDataReader> result,
      CancellationToken cancellationToken = default)
  {
    _commands.Enqueue(command.CommandText);
    return ValueTask.FromResult(result);
  }

  public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
      DbCommand command,
      CommandEventData eventData,
      InterceptionResult<object> result,
      CancellationToken cancellationToken = default)
  {
    _commands.Enqueue(command.CommandText);
    return ValueTask.FromResult(result);
  }

  public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
      DbCommand command,
      CommandEventData eventData,
      InterceptionResult<int> result,
      CancellationToken cancellationToken = default)
  {
    _commands.Enqueue(command.CommandText);
    return ValueTask.FromResult(result);
  }
}
