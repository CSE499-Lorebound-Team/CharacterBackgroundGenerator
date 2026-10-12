using System.Diagnostics;

namespace Lorebound.Api.Logging;

/// <summary>
/// One structured log line per request (P8-05), inside a <c>TraceId</c>
/// scope that covers every other log line the request writes, unhandled
/// exceptions included. It logs the matched <b>route template</b>
/// (<c>api/invites/{code}</c>), never the raw path or query string, so ids,
/// invite codes and emails in URLs stay out of the logs; headers, cookies
/// and bodies are never logged.
/// </summary>
public class RequestLoggingMiddleware
{
  public const string TraceIdScopeKey = "TraceId";

  private readonly RequestDelegate _next;
  private readonly ILogger<RequestLoggingMiddleware> _logger;

  public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
  {
    _next = next;
    _logger = logger;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    using var scope = _logger.BeginScope(new TraceScope(RequestTrace.Id(context)));

    // Routing runs before this middleware, so the endpoint is known here;
    // the exception handler clears it when it re-executes.
    var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
    var started = Stopwatch.GetTimestamp();

    try
    {
      await _next(context);
    }
    finally
    {
      var status = context.Response.StatusCode;
      _logger.Log(
          status >= StatusCodes.Status500InternalServerError ? LogLevel.Error : LogLevel.Information,
          "HTTP {Method} {Route} responded {StatusCode} in {ElapsedMs:0.0} ms",
          context.Request.Method,
          route,
          status,
          Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
  }

  // A key/value pair for structured sinks (the JSON console), and
  // "TraceId:<id>" for the text console, which prints scopes with ToString.
  private sealed class TraceScope(string traceId) : IReadOnlyList<KeyValuePair<string, object?>>
  {
    private readonly KeyValuePair<string, object?> _pair = new(TraceIdScopeKey, traceId);

    public int Count => 1;

    public KeyValuePair<string, object?> this[int index] =>
        index == 0 ? _pair : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
      yield return _pair;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"{TraceIdScopeKey}:{_pair.Value}";
  }
}
