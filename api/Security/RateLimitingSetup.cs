using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Lorebound.Api.Security;

public class AuthRateLimitOptions
{
  public const string SectionName = "RateLimiting:Auth";

  public int PermitLimit { get; set; } = 10;

  public int WindowSeconds { get; set; } = 60;
}

public static class RateLimitingSetup
{
  /// <summary>
  /// Fixed window per client IP, shared by the endpoints that guess
  /// credentials or send email (login, register, forgot-password,
  /// resend-confirmation). Apply with [EnableRateLimiting(AuthPolicy)].
  /// </summary>
  public const string AuthPolicy = "auth";

  /// <summary>
  /// Same limits as <see cref="AuthPolicy"/> but its own counter, for the
  /// endpoints that look up an invite code (P3-04, P3-05), so guessing codes
  /// is slow and does not use up the caller's login attempts.
  /// </summary>
  public const string InvitePolicy = "invites";

  public static IServiceCollection AddLoreboundRateLimiting(this IServiceCollection services)
  {
    // Defaults are 10 requests / 60 seconds; tests raise the limit through
    // RateLimiting:Auth so they do not trip it.
    services
        .AddOptions<AuthRateLimitOptions>()
        .BindConfiguration(AuthRateLimitOptions.SectionName);

    services.AddRateLimiter(options =>
    {
      options.AddPolicy(AuthPolicy, httpContext => PerClientIp(httpContext, AuthPolicy));
      options.AddPolicy(InvitePolicy, httpContext => PerClientIp(httpContext, InvitePolicy));

      options.OnRejected = WriteRejectionAsync;
    });

    // Behind a reverse proxy the client IP arrives in X-Forwarded-For. It is
    // trusted only from loopback and the proxies listed in
    // ForwardedHeaders:KnownProxies, so clients cannot spoof their IP to
    // dodge the limit.
    services
        .AddOptions<ForwardedHeadersOptions>()
        .Configure<IConfiguration>((options, configuration) =>
        {
          options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

          var proxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
          foreach (var proxy in proxies)
          {
            options.KnownProxies.Add(IPAddress.Parse(proxy));
          }
        });

    return services;
  }

  // The policy name is part of the key so each policy counts separately.
  private static RateLimitPartition<string> PerClientIp(HttpContext httpContext, string policy)
  {
    var settings = httpContext.RequestServices
        .GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;

    return RateLimitPartition.GetFixedWindowLimiter(
        $"{policy}:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
        _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = settings.PermitLimit,
          Window = TimeSpan.FromSeconds(settings.WindowSeconds),
          QueueLimit = 0,
        });
  }

  private static async ValueTask WriteRejectionAsync(
      OnRejectedContext context, CancellationToken cancellationToken)
  {
    var httpContext = context.HttpContext;
    var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
        ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
        : httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value.WindowSeconds;

    httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

    var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
    await problemDetails.WriteAsync(new ProblemDetailsContext
    {
      HttpContext = httpContext,
      ProblemDetails = new ProblemDetails
      {
        Status = StatusCodes.Status429TooManyRequests,
        Title = "Too Many Requests",
        Detail = "Too many attempts. Try again later.",
        Extensions = { ["retryAfterSeconds"] = retryAfterSeconds },
      },
    });
  }
}
