using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Lorebound.Api.Tests.TestSupport;

/// <summary>One log line as the API wrote it, with its structured values and scopes.</summary>
public sealed record CapturedLog(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Scopes,
    Exception? Exception)
{
  /// <summary>Every string a log sink could write for this line.</summary>
  public string AllText =>
      string.Join("\n",
          new[] { Category, Message, Exception?.ToString() ?? string.Empty }
              .Concat(Properties.Select(p => $"{p.Key}={p.Value}"))
              .Concat(Scopes.SelectMany(s => s.Select(p => $"{p.Key}={p.Value}"))));

  public object? Scope(string key) =>
      Scopes.Select(s => s.TryGetValue(key, out var value) ? value : null).LastOrDefault(v => v is not null);
}

/// <summary>
/// Records what the API logs, after the configured level filters (so it sees
/// what the console would). A factory and its copies share one capture, but
/// each host gets its own provider from <see cref="CreateProvider"/>, since a
/// provider is bound to its host's scopes. Cleared before every Postgres test
/// (P8-05).
/// </summary>
public sealed class LogCapture
{
  private readonly ConcurrentQueue<CapturedLog> _logs = new();

  public IReadOnlyList<CapturedLog> Logs => _logs.ToList();

  public void Clear() => _logs.Clear();

  public ILoggerProvider CreateProvider() => new Provider(this);

  /// <summary>
  /// Waits for a matching line. The client can get its response before the
  /// server finishes logging the request, so read request logs through this.
  /// </summary>
  public async Task<CapturedLog> WaitForAsync(Func<CapturedLog, bool> match)
  {
    var deadline = DateTime.UtcNow.AddSeconds(10);
    while (DateTime.UtcNow < deadline)
    {
      var found = _logs.FirstOrDefault(match);
      if (found is not null)
      {
        return found;
      }

      await Task.Delay(10);
    }

    throw new TimeoutException("No matching log line within 10 seconds.");
  }

  private static IReadOnlyDictionary<string, object?> ToDictionary(object? state) =>
      state is IEnumerable<KeyValuePair<string, object?>> pairs
          ? pairs.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Last().Value)
          : state is IEnumerable<KeyValuePair<string, object>> objects
              ? objects.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => (object?)g.Last().Value)
              : new Dictionary<string, object?> { ["Scope"] = state?.ToString() };

  private sealed class Provider(LogCapture capture) : ILoggerProvider, ISupportExternalScope
  {
    private readonly LogCapture _capture = capture;
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class Logger(Provider provider, string category) : ILogger
    {
      public IDisposable? BeginScope<TState>(TState state)
          where TState : notnull => provider._scopes.Push(state);

      public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

      public void Log<TState>(
          LogLevel logLevel,
          EventId eventId,
          TState state,
          Exception? exception,
          Func<TState, Exception?, string> formatter)
      {
        var scopes = new List<IReadOnlyDictionary<string, object?>>();
        provider._scopes.ForEachScope((scope, list) => list.Add(ToDictionary(scope)), scopes);

        provider._capture._logs.Enqueue(new CapturedLog(
            category,
            logLevel,
            formatter(state, exception),
            ToDictionary(state),
            scopes,
            exception));
      }
    }
  }
}
