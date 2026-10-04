using Lorebound.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Lorebound.Api.Email;

/// <summary>
/// Development only: writes each email's link or code to the log instead of
/// sending it, so a developer can click it from the terminal. Never register
/// this outside Development, since the links grant account access.
/// </summary>
public class ConsoleEmailSender : IEmailSender<ApplicationUser>
{
  private readonly ILogger<ConsoleEmailSender> _logger;

  public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
  {
    _logger = logger;
  }

  public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
  {
    _logger.LogInformation(
        "[dev email] Confirm the account for user {UserId}: {Link}", user.Id, confirmationLink);
    return Task.CompletedTask;
  }

  public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
  {
    _logger.LogInformation(
        "[dev email] Reset the password for user {UserId}: {Link}", user.Id, resetLink);
    return Task.CompletedTask;
  }

  public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
  {
    _logger.LogInformation(
        "[dev email] Password reset code for user {UserId}: {Code}", user.Id, resetCode);
    return Task.CompletedTask;
  }
}
