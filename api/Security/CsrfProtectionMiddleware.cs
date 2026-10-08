using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Security;

/// <summary>
/// CSRF defense for cookie auth, without a token readable by JavaScript.
/// Every unsafe request (POST, PUT, PATCH, DELETE) must:
/// <list type="bullet">
/// <item>carry <c>X-Requested-With: Lorebound</c>. A cross-origin page can only
/// add a custom header after a CORS preflight, which the CORS allowlist
/// refuses, and an HTML form cannot add headers at all;</item>
/// <item>if it has an <c>Origin</c> header, come from an origin in
/// <c>Cors:AllowedOrigins</c>. This also stops same-site pages on other
/// origins, which SameSite=Lax would let through.</item>
/// </list>
/// Safe methods and OPTIONS pass untouched.
/// </summary>
public class CsrfProtectionMiddleware
{
  public const string HeaderName = "X-Requested-With";
  public const string HeaderValue = "Lorebound";

  private readonly RequestDelegate _next;
  private readonly IConfiguration _configuration;

  public CsrfProtectionMiddleware(RequestDelegate next, IConfiguration configuration)
  {
    _next = next;
    _configuration = configuration;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    var request = context.Request;
    var unsafeMethod = HttpMethods.IsPost(request.Method)
        || HttpMethods.IsPut(request.Method)
        || HttpMethods.IsPatch(request.Method)
        || HttpMethods.IsDelete(request.Method);

    if (unsafeMethod)
    {
      if (!string.Equals(request.Headers[HeaderName], HeaderValue, StringComparison.Ordinal))
      {
        await RejectAsync(context, $"Missing or invalid {HeaderName} header.");
        return;
      }

      var origin = request.Headers.Origin.ToString();
      if (origin.Length > 0 && !IsAllowedOrigin(origin))
      {
        await RejectAsync(context, "Origin not allowed.");
        return;
      }
    }

    await _next(context);
  }

  private bool IsAllowedOrigin(string origin)
  {
    // Read per request so config overrides (and tests) apply; the list is tiny.
    var allowed = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    return allowed.Any(entry =>
        string.Equals(entry.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
  }

  private static async Task RejectAsync(HttpContext context, string detail)
  {
    context.Response.StatusCode = StatusCodes.Status403Forbidden;

    var problemDetails = context.RequestServices.GetRequiredService<IProblemDetailsService>();
    await problemDetails.WriteAsync(new ProblemDetailsContext
    {
      HttpContext = context,
      ProblemDetails = new ProblemDetails
      {
        Status = StatusCodes.Status403Forbidden,
        Title = "Forbidden",
        Detail = detail,
      },
    });
  }
}
