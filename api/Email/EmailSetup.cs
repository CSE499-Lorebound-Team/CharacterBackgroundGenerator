using Lorebound.Api.Auth;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Lorebound.Api.Email;

public static class EmailSetup
{
  /// <summary>
  /// Registers the email sender and the link builder. Development logs emails
  /// with <see cref="ConsoleEmailSender"/>.
  ///
  /// Extension point: to send real email, implement
  /// IEmailSender&lt;ApplicationUser&gt; for the chosen SMTP/API provider, bind
  /// its settings from an "Email" config section (secrets via environment
  /// variables), and register it here in place of
  /// <see cref="UnconfiguredEmailSender"/>.
  /// </summary>
  public static IServiceCollection AddLoreboundEmail(
      this IServiceCollection services,
      IHostEnvironment environment)
  {
    services
        .AddOptions<AppOptions>()
        .BindConfiguration(AppOptions.SectionName)
        .ValidateDataAnnotations();
    services.AddSingleton<FrontendLinks>();

    if (environment.IsDevelopment())
    {
      services.AddSingleton<IEmailSender<ApplicationUser>, ConsoleEmailSender>();
    }
    else
    {
      services.AddSingleton<IEmailSender<ApplicationUser>, UnconfiguredEmailSender>();
    }

    return services;
  }
}
