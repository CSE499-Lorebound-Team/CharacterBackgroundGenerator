namespace Lorebound.Api.Security;

/// <summary>
/// HSTS outside Development (P8-07). ASP.NET Core's <c>UseHsts</c> only adds
/// the header to requests that arrive over HTTPS, but in production TLS ends
/// at Render's proxy and the API sees plain HTTP: <c>X-Forwarded-Proto</c> is
/// not trusted, because the proxies' IPs cannot be pinned (ADR 0003). So the
/// header goes on every response. That is safe: browsers ignore it on a
/// response that did not come over HTTPS (RFC 6797, section 8.1).
/// <c>OnStarting</c> adds it after the exception handler has cleared the
/// headers of a failed response, so error responses carry it too.
/// </summary>
public class StrictTransportSecurityMiddleware
{
  public const string HeaderValue = "max-age=31536000";

  private readonly RequestDelegate _next;

  public StrictTransportSecurityMiddleware(RequestDelegate next)
  {
    _next = next;
  }

  public Task InvokeAsync(HttpContext context)
  {
    context.Response.OnStarting(() =>
    {
      context.Response.Headers.StrictTransportSecurity = HeaderValue;
      return Task.CompletedTask;
    });

    return _next(context);
  }
}
