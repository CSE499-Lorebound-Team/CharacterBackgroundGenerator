using Lorebound.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Lorebound.Api.Email;

/// <summary>
/// Sends nothing: email is turned off for the course demo (P8-09, ADR 0003).
/// Used outside Development only when <c>Email:Provider</c> is <c>None</c>.
/// It logs which kind of email was skipped and for which user id, never the
/// link, code or address: in production those grant account access.
/// </summary>
public class DisabledEmailSender : IEmailSender<ApplicationUser>
{
  private readonly ILogger<DisabledEmailSender> _logger;

  public DisabledEmailSender(ILogger<DisabledEmailSender> logger)
  {
    _logger = logger;
  }

  public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
      Skip("confirmation link", user);

  public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
      Skip("password reset link", user);

  public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
      Skip("password reset code", user);

  private Task Skip(string kind, ApplicationUser user)
  {
    _logger.LogInformation(
        "Email is turned off (Email:Provider=None); no {Kind} was sent to user {UserId}",
        kind, user.Id);
    return Task.CompletedTask;
  }
}
