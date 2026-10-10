using System.Diagnostics;

namespace Lorebound.Api.Logging;

/// <summary>
/// The request's correlation id: the <c>traceId</c> in every problem+json
/// body and the <c>TraceId</c> scope on every log line written while the
/// request runs (P8-05). One definition, so a client's error body always
/// finds the matching log lines.
/// </summary>
public static class RequestTrace
{
  public static string Id(HttpContext context) =>
      Activity.Current?.Id ?? context.TraceIdentifier;
}
