using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Email;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.Email;

// P8-09: the course demo runs in Production with email off (ADR 0003,
// "Email"). Every auth endpoint still works; nothing is sent, and no link,
// code or address reaches the logs.
[Collection(PostgresCollection.Name)]
public class EmailOffTests : PostgresTestBase
{
  private const string Email = "demo.player@example.com";
  private const string Password = "a demo passphrase";

  public EmailOffTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  // Production as Render runs it (ADR 0003), with the API's own sender
  // instead of the test factory's recording one.
  private WebApplicationFactory<Program> DemoHost() =>
      Factory.WithWebHostBuilder(builder =>
      {
        builder.UseEnvironment("Production");
        builder.UseSetting(EmailSetup.ProviderKey, EmailSetup.NoProvider);
        builder.UseSetting("Auth:RequireConfirmedEmail", "false");
        builder.UseSetting("App:FrontendBaseUrl", "https://lorebound.vercel.app");
        builder.ConfigureServices(services =>
        {
          foreach (var recording in services
              .Where(service => service.ServiceType == typeof(IEmailSender<ApplicationUser>)
                  && service.ImplementationInstance is TestEmailSender)
              .ToList())
          {
            services.Remove(recording);
          }
        });
      });

  [Fact]
  public async Task Register_login_and_the_email_endpoints_work_without_sending_email()
  {
    using var host = DemoHost();
    Assert.IsType<DisabledEmailSender>(host.Services.GetRequiredService<IEmailSender<ApplicationUser>>());
    var client = host.CreateCookieClient();

    var register = await client.PostAsJsonAsync(
        "/api/auth/register", new { email = Email, password = Password, displayName = "Demo Player" });
    Assert.Equal(HttpStatusCode.Created, register.StatusCode);

    // Signs in without confirming the email.
    var login = await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password });
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

    var resend = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email = Email });
    Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);

    // Forgot-password only emails confirmed accounts; confirm this one so the
    // sender is really called. A variable, not the constant, so EF sends it
    // as a parameter: a constant would be inlined into the logged SQL.
    var email = Email;
    await Factory.WithDbAsync(async db =>
    {
      var user = db.Users.Single(u => u.Email == email);
      user.EmailConfirmed = true;
      return await db.SaveChangesAsync();
    });
    var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });
    Assert.Equal(HttpStatusCode.NoContent, forgot.StatusCode);

    Assert.Empty(Factory.Emails.Sent);

    // The sender ran (register, resend and forgot) and logged no secrets.
    var skipped = Factory.Logs.Logs.Where(log => log.Category == typeof(DisabledEmailSender).FullName).ToList();
    Assert.Equal(3, skipped.Count);
    var allText = string.Join("\n", Factory.Logs.Logs.Select(log => log.AllText));
    Assert.DoesNotContain(Email, allText, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("code=", allText);
    Assert.DoesNotContain("/confirm-email", allText);
    Assert.DoesNotContain("/reset-password", allText);
  }
}
