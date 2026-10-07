using Lorebound.Api.Data;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Auth;

public static class AuthenticationSetup
{
  public const string CookieName = "lorebound.auth";

  /// <summary>
  /// Registers ASP.NET Core Identity with a cookie scheme only. The API never
  /// issues bearer or refresh tokens, so do not add MapIdentityApi or
  /// AddIdentityApiEndpoints: their login endpoint can return tokens in the
  /// JSON body.
  /// </summary>
  public static IServiceCollection AddLoreboundAuthentication(
      this IServiceCollection services)
  {
    services
        .AddIdentityCore<ApplicationUser>(options =>
        {
          // Length over complexity: no forced digit, case or symbol rules.
          options.Password.RequiredLength = 10;
          options.Password.RequireDigit = false;
          options.Password.RequireLowercase = false;
          options.Password.RequireUppercase = false;
          options.Password.RequireNonAlphanumeric = false;

          options.User.RequireUniqueEmail = true;
          // The username is the email; the default character allowlist
          // rejects valid addresses such as o'brien@example.com.
          options.User.AllowedUserNameCharacters = string.Empty;

          options.Lockout.AllowedForNewUsers = true;
          options.Lockout.MaxFailedAccessAttempts = 5;
          options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<LoreboundDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders()
        .AddClaimsPrincipalFactory<LoreboundClaimsPrincipalFactory>();

    // Read when the options are first used, not now, so every config source
    // (including test overrides added at build time) is seen. Missing config
    // fails safe: confirmation is required unless a config file
    // (appsettings.Development.json) turns it off.
    services
        .AddOptions<IdentityOptions>()
        .Configure<IConfiguration>((options, configuration) =>
            options.SignIn.RequireConfirmedEmail =
                configuration.GetValue("Auth:RequireConfirmedEmail", defaultValue: true));

    services
        .AddAuthentication(IdentityConstants.ApplicationScheme)
        .AddIdentityCookies();

    // Check the cookie's security stamp against the database on every
    // request (default: every 30 minutes), so a password reset ends the
    // user's other sessions immediately. Costs one user lookup per signed-in
    // request.
    services.Configure<SecurityStampValidatorOptions>(options =>
        options.ValidationInterval = TimeSpan.Zero);

    services.ConfigureApplicationCookie(options =>
    {
      options.Cookie.Name = CookieName;
      options.Cookie.HttpOnly = true;
      options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
      options.Cookie.SameSite = SameSiteMode.Lax;
      options.ExpireTimeSpan = TimeSpan.FromDays(14);
      options.SlidingExpiration = true;

      // An API has no login page to redirect to.
      options.Events.OnRedirectToLogin = context =>
          WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized");
      options.Events.OnRedirectToAccessDenied = context =>
          WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Forbidden");
    });

    // Secure by default: every endpoint needs a signed-in user unless it is
    // marked [AllowAnonymous] (or .AllowAnonymous() for minimal endpoints).
    // Requests that match no endpoint also get 401 when anonymous.
    services
        .AddAuthorizationBuilder()
        .SetFallbackPolicy(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

    services.AddHttpContextAccessor();
    services.AddScoped<ICurrentUser, CurrentUser>();
    services.AddScoped<ISettingAccess, SettingAccess>();

    return services;
  }

  private static async Task WriteProblemAsync(
      RedirectContext<CookieAuthenticationOptions> context,
      int status,
      string title)
  {
    var httpContext = context.HttpContext;
    httpContext.Response.StatusCode = status;

    var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
    await problemDetails.WriteAsync(new ProblemDetailsContext
    {
      HttpContext = httpContext,
      ProblemDetails = new ProblemDetails { Status = status, Title = title },
    });
  }
}
