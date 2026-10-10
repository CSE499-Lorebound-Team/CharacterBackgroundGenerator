using Lorebound.Api.Auth;
using Lorebound.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace Lorebound.Api.OpenApi;

/// <summary>
/// The OpenAPI document (P8-03), served at <c>/openapi/v1.json</c> and
/// browsable with Scalar at <c>/scalar</c> in Development. Actions declare
/// their own success and domain statuses with <c>[ProducesResponseType]</c>
/// and describe themselves with XML doc comments. The cross-cutting rules
/// below are derived from endpoint metadata instead, so they cannot drift:
/// <list type="bullet">
/// <item>401 and the cookie security requirement on every endpoint that is
/// not <c>[AllowAnonymous]</c> (the fallback policy signs everything else
/// in);</item>
/// <item>the required <c>X-Requested-With: Lorebound</c> header and its 403
/// on every POST, PUT, PATCH and DELETE (CSRF check);</item>
/// <item>429 on every rate-limited endpoint;</item>
/// <item>JSON only: success bodies as <c>application/json</c>, errors as
/// <c>application/problem+json</c> (400 with validation errors).</item>
/// </list>
/// </summary>
public static class OpenApiSetup
{
  public const string CookieScheme = "cookieAuth";

  private const string Json = "application/json";
  private const string ProblemJson = "application/problem+json";

  private const string Description = """
      The Lorebound API behind the frontend: campaign settings, their lore entries and
      relationships, sharing by invite, and characters built step by step.

      ## Authentication: a cookie, no tokens
      1. `POST /api/auth/login` with `{ "email", "password" }` sets the httpOnly
         `lorebound.auth` cookie (Secure, SameSite=Lax, 14 days, sliding).
      2. Send it with every later request (`credentials: "include"` in `fetch`). The API
         never returns bearer or refresh tokens.
      3. `POST /api/auth/logout` clears it.

      Every endpoint requires the cookie unless it is marked as not needing it here;
      without it the response is **401** problem+json.

      ## CSRF: the `X-Requested-With` header
      Every **POST, PUT, PATCH and DELETE** must send `X-Requested-With: Lorebound`,
      and, if it carries an `Origin`, come from an allowed frontend origin. Otherwise the
      response is **403** before the endpoint runs. GET needs neither.

      ## Errors
      Every error is RFC 7807 `application/problem+json` with a `traceId`; quote it when
      reporting a problem, it finds the request in the server logs. Invalid input is
      **400** with an `errors` object keyed by field.

      ## Visibility
      A setting you do not belong to is **404**, exactly like a missing one. Players never
      see GM-only entries, nor relationships that touch them.
      """;

  public static IServiceCollection AddLoreboundOpenApi(this IServiceCollection services) =>
      services.AddOpenApi(options =>
      {
        options.AddDocumentTransformer((document, _, _) =>
        {
          document.Info.Title = "Lorebound API";
          document.Info.Description = Description;

          document.Components ??= new OpenApiComponents();
          document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
          document.Components.SecuritySchemes[CookieScheme] = new OpenApiSecurityScheme
          {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = AuthenticationSetup.CookieName,
            Description = "Set by POST /api/auth/login; httpOnly, so browsers send it automatically.",
          };

          return Task.CompletedTask;
        });

        options.AddOperationTransformer(TransformOperationAsync);
      });

  private static async Task TransformOperationAsync(
      OpenApiOperation operation,
      OpenApiOperationTransformerContext context,
      CancellationToken cancellationToken)
  {
    var metadata = context.Description.ActionDescriptor.EndpointMetadata;
    var method = context.Description.HttpMethod ?? string.Empty;
    operation.Responses ??= new OpenApiResponses();

    if (!metadata.OfType<IAllowAnonymous>().Any())
    {
      operation.Security ??= [];
      operation.Security.Add(new OpenApiSecurityRequirement
      {
        [new OpenApiSecuritySchemeReference(CookieScheme, context.Document)] = [],
      });
      AddResponse(operation, StatusCodes.Status401Unauthorized, "Not signed in.");
    }

    if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
    {
      operation.Parameters ??= [];
      operation.Parameters.Add(new OpenApiParameter
      {
        Name = CsrfProtectionMiddleware.HeaderName,
        In = ParameterLocation.Header,
        Required = true,
        Description = "CSRF check: must be exactly `Lorebound`.",
        Schema = new OpenApiSchema
        {
          Type = JsonSchemaType.String,
          Enum = [System.Text.Json.Nodes.JsonValue.Create(CsrfProtectionMiddleware.HeaderValue)!],
        },
      });
      AddResponse(
          operation,
          StatusCodes.Status403Forbidden,
          "Forbidden, including a missing X-Requested-With header or a disallowed Origin.");
    }

    if (metadata.OfType<EnableRateLimitingAttribute>().Any())
    {
      AddResponse(operation, StatusCodes.Status429TooManyRequests, "Too many requests; retry later.");
    }

    // JSON only, and errors as problem+json, which is what the API sends.
    if (operation.RequestBody?.Content is { } requestContent)
    {
      foreach (var type in requestContent.Keys.Where(type => type != Json).ToList())
      {
        requestContent.Remove(type);
      }
    }

    var problem = new OpenApiSchemaReference(nameof(ProblemDetails), context.Document);
    var validationProblem = await context.GetOrCreateSchemaAsync(
        typeof(HttpValidationProblemDetails), null, cancellationToken);

    foreach (var (status, response) in operation.Responses)
    {
      if (response is not OpenApiResponse concrete)
      {
        continue;
      }

      // An error status is problem+json unless the action declared its own
      // body type (health's 503 returns HealthResponse).
      var declaresOwnBody = concrete.Content?.Values.Any(media =>
          media.Schema is OpenApiSchemaReference reference
          && reference.Reference.Id != nameof(ProblemDetails)) == true;

      if (int.TryParse(status, out var code) && code >= 400 && !declaresOwnBody)
      {
        concrete.Content = new Dictionary<string, OpenApiMediaType>
        {
          [ProblemJson] = new()
          {
            Schema = code == StatusCodes.Status400BadRequest ? validationProblem : problem,
          },
        };
      }
      else if (concrete.Content is { Count: > 0 } content)
      {
        foreach (var type in content.Keys.Where(type => type != Json).ToList())
        {
          content.Remove(type);
        }
      }
    }
  }

  private static void AddResponse(OpenApiOperation operation, int status, string description)
  {
    var key = status.ToString();
    if (operation.Responses!.TryGetValue(key, out var existing))
    {
      if (existing is OpenApiResponse concrete && string.IsNullOrEmpty(concrete.Description))
      {
        concrete.Description = description;
      }

      return;
    }

    operation.Responses[key] = new OpenApiResponse { Description = description };
  }
}
