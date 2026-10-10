using System.Text.Json.Serialization;
using Lorebound.Api.Auth;
using Lorebound.Api.Controllers;
using Lorebound.Api.Data;
using Lorebound.Api.Email;
using Lorebound.Api.Errors;
using Lorebound.Api.Logging;
using Lorebound.Api.OpenApi;
using Scalar.AspNetCore;
using Lorebound.Api.Security;
using Microsoft.EntityFrameworkCore;

// `dotnet run -- --seed` turns on the Development seed data (P8-02). The
// bare flag is removed first: the command-line config provider expects a
// value after it.
var seed = args.Contains(DevelopmentSeedingSetup.CommandLineFlag);
var builder = WebApplication.CreateBuilder(
    args.Where(arg => arg != DevelopmentSeedingSetup.CommandLineFlag).ToArray());
if (seed)
{
    builder.Configuration[DevelopmentSeedingSetup.EnabledKey] = "true";
}

// Logs go to stdout only, as structured JSON outside Development (P8-05;
// see Logging in appsettings*.json). The Windows Event Log provider is
// dropped: its own filter would re-enable ASP.NET Core's hosting logger,
// whose request scope carries the raw path (invite codes, ids) into every
// log line.
builder.Logging.ClearProviders().AddConsole().AddDebug();

// Add services to the container.
// The OpenAPI document and its cross-cutting rules (P8-03); see OpenApiSetup.
builder.Services.AddLoreboundOpenApi();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        // Enums travel as names ("Location"), not numbers.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Every error response is RFC 7807 problem+json carrying a traceId, the
// same id as the TraceId scope on that request's log lines (P8-05).
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = RequestTrace.Id(context.HttpContext));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Explicit origin allowlist from Cors:AllowedOrigins (in production, set
// Cors__AllowedOrigins__0, __1, ...). Credentials are allowed so the auth
// cookie is sent, which is why a wildcard origin is never used.
var allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowCredentials()
            // Echoes the requested headers, which covers X-Requested-With for
            // the CSRF check (P1-10; CorsTests asserts it). Adding WithHeaders()
            // here would disable any-header and block Content-Type.
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton(TimeProvider.System);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. For local " +
        "development run, from the api folder: dotnet user-secrets set " +
        "\"ConnectionStrings:DefaultConnection\" \"Host=localhost;Port=5432;" +
        "Database=lorebound;Username=lorebound;Password=<your .env password>\". " +
        "Elsewhere set the ConnectionStrings__DefaultConnection environment variable.");
}

builder.Services.AddDbContext<LoreboundDbContext>(options =>
    options.UseNpgsql(connectionString));

// GET /api/health reports this check (P8-05).
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<LoreboundDbContext>(HealthController.DatabaseCheck);

// Identity with the httpOnly lorebound.auth cookie; no tokens anywhere.
builder.Services.AddLoreboundAuthentication();
builder.Services.AddLoreboundEmail(builder.Environment);
builder.Services.AddLoreboundRateLimiting();
builder.Services.AddDevelopmentSeeding();

var app = builder.Build();

// Configure the HTTP request pipeline.
// First, so everything after sees the real client IP and scheme behind a
// trusted proxy (see RateLimitingSetup).
app.UseForwardedHeaders();
// Before the exception handler, so its error logs carry the TraceId scope
// and the logged status is the final one.
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
// Bodyless error statuses (e.g. unmatched routes) also become problem+json.
// The auth cookie writes its own 401/403 problem bodies.
app.UseStatusCodePages();

app.UseHttpsRedirection();
app.UseCors("Frontend");
// After CORS, which answers preflights first; before everything else, so a
// forged request is refused before it reaches the rate limiter or auth.
app.UseMiddleware<CsrfProtectionMiddleware>();
// After CORS (so 429s carry CORS headers) and before authentication, so a
// throttled request costs no database lookups.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    // API explorer at /scalar (Development only). "Try it" runs in this
    // origin, so sign in with POST /api/auth/login first and the cookie is
    // sent; writes need the X-Requested-With header the document declares.
    app.MapScalarApiReference(options => options.WithTitle("Lorebound API")).AllowAnonymous();
}

app.MapControllers();

app.Run();