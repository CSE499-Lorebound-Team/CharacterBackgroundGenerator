using Lorebound.Api.Auth;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Lorebound.Api.Email;

public static class EmailSetup
{
  /// <summary>Which sender to use outside Development (P8-09).</summary>
  public const string ProviderKey = "Email:Provider";

  /// <summary><see cref="ProviderKey"/> value that turns email off.</summary>
  public const string NoProvider = "None";

  /// <summary>
  /// Registers the email sender and the link builder. Development logs emails
  /// with <see cref="ConsoleEmailSender"/>. Elsewhere, <c>Email:Provider=None</c>
  /// selects <see cref="DisabledEmailSender"/> (the course demo, ADR 0003);
  /// anything else keeps <see cref="UnconfiguredEmailSender"/>, so a missing
  /// setting fails loudly instead of silently dropping email.
  ///
  /// Extension point: to send real email, implement
  /// IEmailSender&lt;ApplicationUser&gt; for the chosen SMTP/API provider, bind
  /// its settings from the "Email" config section (secrets via environment
  /// variables), and select it here by its <c>Email:Provider</c> value.
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
      // Chosen when first resolved, not now, so every config source
      // (including test overrides added at build time) is seen.
      services.AddSingleton<IEmailSender<ApplicationUser>>(provider =>
          string.Equals(
              provider.GetRequiredService<IConfiguration>()[ProviderKey]?.Trim(),
              NoProvider,
              StringComparison.OrdinalIgnoreCase)
              ? ActivatorUtilities.CreateInstance<DisabledEmailSender>(provider)
              : new UnconfiguredEmailSender());
    }

    return services;
  }
}
