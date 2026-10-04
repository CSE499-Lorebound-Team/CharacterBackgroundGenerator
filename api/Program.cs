using System.Diagnostics;
using System.Text.Json.Serialization;
using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Email;
using Lorebound.Api.Errors;
using Lorebound.Api.Security;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        // Enums travel as names ("Location"), not numbers.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Every error response is RFC 7807 problem+json carrying a traceId.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
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

// Identity with the httpOnly lorebound.auth cookie; no tokens anywhere.
builder.Services.AddLoreboundAuthentication();
builder.Services.AddLoreboundEmail(builder.Environment);
builder.Services.AddLoreboundRateLimiting();

var app = builder.Build();

// Configure the HTTP request pipeline.
// First, so everything after sees the real client IP and scheme behind a
// trusted proxy (see RateLimitingSetup).
app.UseForwardedHeaders();
app.UseExceptionHandler();
// Bodyless error statuses (e.g. unmatched routes) also become problem+json.
// The auth cookie writes its own 401/403 problem bodies.
app.UseStatusCodePages();

app.UseHttpsRedirection();
app.UseCors("Frontend");
// After CORS (so 429s carry CORS headers) and before authentication, so a
// throttled request costs no database lookups.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapControllers();

app.Run();