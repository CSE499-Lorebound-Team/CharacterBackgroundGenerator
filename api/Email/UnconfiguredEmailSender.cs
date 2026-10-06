using Lorebound.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Lorebound.Api.Email;

/// <summary>
/// Used outside Development until a real provider is chosen. It fails loudly
/// rather than silently dropping confirmation and reset emails.
/// </summary>
public class UnconfiguredEmailSender : IEmailSender<ApplicationUser>
{
  public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
      throw NotConfigured();

  public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
      throw NotConfigured();

  public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
      throw NotConfigured();

  private static InvalidOperationException NotConfigured() =>
      new("No email provider is configured. Register an IEmailSender<ApplicationUser> " +
          "in Email/EmailSetup.cs (see \"Email\" in api/README.md).");
}
