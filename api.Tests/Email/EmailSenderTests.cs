using Lorebound.Api.Auth;
using Lorebound.Api.Email;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lorebound.Api.Tests.Email;

public class EmailSenderTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;

  public EmailSenderTests(ApiFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public void Development_uses_the_console_sender()
  {
    var sender = _factory.Services.GetRequiredService<IEmailSender<ApplicationUser>>();

    Assert.IsType<ConsoleEmailSender>(sender);
  }

  [Fact]
  public async Task Outside_development_an_unconfigured_sender_fails_loudly()
  {
    var services = _factory
        .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
        .Services;
    var sender = services.GetRequiredService<IEmailSender<ApplicationUser>>();

    Assert.IsType<UnconfiguredEmailSender>(sender);
    var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        sender.SendConfirmationLinkAsync(new ApplicationUser(), "a@example.com", "https://x"));
    Assert.Contains("No email provider is configured", error.Message);
  }

  [Fact]
  public async Task Console_sender_logs_the_full_link_without_the_email()
  {
    var logger = new RecordingLogger<ConsoleEmailSender>();
    var sender = new ConsoleEmailSender(logger);
    var user = new ApplicationUser { Id = Guid.NewGuid() };
    const string link = "http://localhost:3000/confirm-email?userId=1&code=abc";

    await sender.SendConfirmationLinkAsync(user, "secret.person@example.com", link);
    await sender.SendPasswordResetLinkAsync(user, "secret.person@example.com", link + "2");

    Assert.Equal(2, logger.Messages.Count);
    Assert.Contains(link, logger.Messages[0]);
    Assert.Contains(user.Id.ToString(), logger.Messages[0]);
    Assert.Contains(link + "2", logger.Messages[1]);
    Assert.All(logger.Messages, message => Assert.DoesNotContain("secret.person", message));
  }

  [Fact]
  public void Confirm_link_points_at_the_frontend_page_with_an_encoded_code()
  {
    var links = _factory.Services.GetRequiredService<FrontendLinks>();
    var userId = Guid.NewGuid();
    const string token = "CfDJ8+token/with=symbols";

    var link = new Uri(links.ConfirmEmail(userId, token));

    Assert.Equal("http://localhost:3000/confirm-email", link.GetLeftPart(UriPartial.Path));
    var query = QueryHelpers.ParseQuery(link.Query);
    Assert.Equal(userId.ToString(), query["userId"].ToString());
    Assert.True(EmailCodes.TryDecode(query["code"].ToString(), out var decoded));
    Assert.Equal(token, decoded);
  }

  [Fact]
  public void Reset_link_points_at_the_frontend_page_with_email_and_code()
  {
    var links = _factory.Services.GetRequiredService<FrontendLinks>();

    var link = new Uri(links.ResetPassword("o'brien+x@example.com", "reset/token"));

    Assert.Equal("http://localhost:3000/reset-password", link.GetLeftPart(UriPartial.Path));
    var query = QueryHelpers.ParseQuery(link.Query);
    Assert.Equal("o'brien+x@example.com", query["email"].ToString());
    Assert.True(EmailCodes.TryDecode(query["code"].ToString(), out var decoded));
    Assert.Equal("reset/token", decoded);
  }

  [Theory]
  [InlineData("not base64 !!")]
  [InlineData("a")]
  public void TryDecode_rejects_malformed_codes(string code)
  {
    Assert.False(EmailCodes.TryDecode(code, out var token));
    Assert.Null(token);
  }

  private sealed class RecordingLogger<T> : ILogger<T>
  {
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Messages.Add(formatter(state, exception));
  }
}
