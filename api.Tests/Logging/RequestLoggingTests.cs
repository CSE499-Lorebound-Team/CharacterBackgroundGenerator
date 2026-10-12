using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Controllers;
using Lorebound.Api.Logging;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Logging;

// P8-05: request logging with a traceId that matches ProblemDetails, no
// secrets in logs, and the database health check.
[Collection(PostgresCollection.Name)]
public class RequestLoggingTests : PostgresTestBase
{
  public RequestLoggingTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private IReadOnlyList<CapturedLog> RequestLogs() =>
      Factory.Logs.Logs.Where(l => l.Category == typeof(RequestLoggingMiddleware).FullName).ToList();

  // Waits for the request line, which can land just after the response.
  private Task<CapturedLog> RequestLogAsync(string route) =>
      Factory.Logs.WaitForAsync(l =>
          l.Category == typeof(RequestLoggingMiddleware).FullName && (string?)l.Properties["Route"] == route);

  private static async Task<string> TraceIdAsync(HttpResponseMessage response) =>
      (await JsonAssert.ReadJsonAsync(response)).GetProperty("traceId").GetString()!;

  [Fact]
  public async Task Each_request_logs_its_route_template_status_and_the_problem_traceId()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var missing = Guid.NewGuid();
    Factory.Logs.Clear();

    var response = await world[Caller.Player].GetAsync($"/api/settings/{missing}");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    var traceId = await TraceIdAsync(response);
    var log = await RequestLogAsync("api/settings/{id:guid}");
    Assert.Single(RequestLogs(), l => Equals(l.Scope(RequestLoggingMiddleware.TraceIdScopeKey), traceId));
    Assert.Equal(LogLevel.Information, log.Level);
    Assert.Equal("GET", log.Properties["Method"]);
    Assert.Equal("api/settings/{id:guid}", log.Properties["Route"]);
    Assert.Equal(404, log.Properties["StatusCode"]);
    Assert.Equal(traceId, log.Scope(RequestLoggingMiddleware.TraceIdScopeKey));
    Assert.DoesNotContain(missing.ToString(), log.AllText);
  }

  [Fact]
  public async Task Every_log_line_of_a_request_carries_its_traceId_and_no_raw_path()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    // The seed's last request (creating the setting) must finish logging
    // before the slate is wiped, or its line would count as ours.
    await RequestLogAsync("api/settings");
    Factory.Logs.Clear();

    var response = await world[Caller.Owner].GetAsync("/api/dashboard");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    await RequestLogAsync("api/dashboard");
    var logs = Factory.Logs.Logs;
    Assert.Contains(logs, l => l.Category.StartsWith("Microsoft.EntityFrameworkCore"));
    var traceIds = logs.Select(l => l.Scope(RequestLoggingMiddleware.TraceIdScopeKey)).Distinct().ToList();
    Assert.Single(traceIds);
    Assert.NotNull(traceIds[0]);
    // ASP.NET Core's own request scope (RequestPath) is switched off.
    Assert.DoesNotContain(logs, l => l.Scopes.Any(s => s.ContainsKey("RequestPath")));
  }

  [Fact]
  public async Task An_unmatched_route_logs_no_path()
  {
    var response = await Factory.CreateCookieClient().GetAsync("/api/no-such-route/secret-looking-value");

    // The fallback policy answers an anonymous unmatched request with 401.
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var log = await RequestLogAsync("(unmatched)");
    Assert.Equal("(unmatched)", log.Properties["Route"]);
    Assert.Equal(401, log.Properties["StatusCode"]);
    Assert.DoesNotContain(Factory.Logs.Logs, l => l.AllText.Contains("secret-looking-value"));
  }

  [Fact]
  public async Task An_unhandled_exception_is_logged_with_the_same_traceId_as_the_500()
  {
    var response = await Factory.CreateCookieClient().GetAsync("/test/errors/unhandled");

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    var traceId = await TraceIdAsync(response);
    var request = await RequestLogAsync("test/errors/unhandled");
    var error = Assert.Single(Factory.Logs.Logs, l => l.Exception is InvalidOperationException);
    Assert.Equal(traceId, error.Scope(RequestLoggingMiddleware.TraceIdScopeKey));
    Assert.Equal(LogLevel.Error, request.Level);
    Assert.Equal(500, request.Properties["StatusCode"]);
    Assert.Equal(traceId, request.Scope(RequestLoggingMiddleware.TraceIdScopeKey));
  }

  [Fact]
  public async Task Passwords_cookies_invite_codes_and_emails_never_reach_the_logs()
  {
    const string email = "secret.person@example.com";
    const string password = "a very private passphrase";
    var anonymous = Factory.CreateCookieClient();
    Assert.Equal(HttpStatusCode.Created, (await anonymous.PostAsJsonAsync(
        "/api/auth/register", new { email, password, displayName = "Secret Person" })).StatusCode);

    // Read the auth cookie's value from a client that does not swallow Set-Cookie.
    var raw = Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      BaseAddress = new Uri("https://localhost"),
      HandleCookies = false,
    });
    var login = await raw.PostAsJsonAsync("/api/auth/login", new { email, password });
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("lorebound.auth="));
    var cookieValue = cookie.Split(';')[0]["lorebound.auth=".Length..];

    await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong " + password });
    await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email });

    var world = await SharingWorld.SeedAsync(Factory);
    var invite = (await world.AddInvitesAsync(1))[0];
    await world[Caller.Anonymous].GetAsync($"/api/invites/{invite.Code}");
    await world[Caller.NonMember].PostAsync($"/api/invites/{invite.Code}/accept", null);
    var created = await world[Caller.Owner].PostAsJsonAsync(
        $"/api/settings/{world.SettingId}/invites", new { expiresInDays = 7 });
    var newCode = (await JsonAssert.ReadJsonAsync(created)).GetProperty("code").GetString()!;

    await RequestLogAsync("api/invites/{code}/accept");
    await RequestLogAsync("api/settings/{settingId:guid}/invites");
    var logs = Factory.Logs.Logs;
    foreach (var secret in new[] { email, password, cookieValue, invite.Code, newCode, "owner@example.com" })
    {
      Assert.DoesNotContain(logs, l => l.AllText.Contains(secret, StringComparison.OrdinalIgnoreCase));
    }
  }

  [Fact]
  public void Health_uses_the_DbContext_check()
  {
    var options = Factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

    Assert.Contains(options.Registrations, r => r.Name == HealthController.DatabaseCheck);
  }
}
