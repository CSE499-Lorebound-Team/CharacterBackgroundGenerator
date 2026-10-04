using System.Collections.Concurrent;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Lorebound.Api.Tests.TestSupport;

public enum EmailKind
{
  ConfirmationLink,
  PasswordResetLink,
  PasswordResetCode,
}

/// <param name="Content">The link, or the code for a reset code.</param>
public record SentEmail(EmailKind Kind, Guid UserId, string To, string Content);

/// <summary>
/// Records emails instead of sending them, so tests can read the link.
/// <see cref="PostgresTestBase"/> clears it before each test.
/// </summary>
public sealed class TestEmailSender : IEmailSender<ApplicationUser>
{
  private readonly ConcurrentQueue<SentEmail> _sent = new();

  public IReadOnlyList<SentEmail> Sent => _sent.ToArray();

  public void Clear() => _sent.Clear();

  public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
      Record(EmailKind.ConfirmationLink, user, email, confirmationLink);

  public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
      Record(EmailKind.PasswordResetLink, user, email, resetLink);

  public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
      Record(EmailKind.PasswordResetCode, user, email, resetCode);

  private Task Record(EmailKind kind, ApplicationUser user, string email, string content)
  {
    _sent.Enqueue(new SentEmail(kind, user.Id, email, content));
    return Task.CompletedTask;
  }
}
