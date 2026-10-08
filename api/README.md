# Lorebound API conventions

Every endpoint follows these rules so the frontend sees one consistent contract.

## JSON

- Property names are camelCase (the ASP.NET Core default).
- Enums are sent and received as their names, e.g. `"entryType": "Location"`,
  never `0`. `JsonStringEnumConverter` is registered globally in `Program.cs`.
- Timestamps are `DateTimeOffset` in UTC (ISO 8601 with offset).

## Database

- Every enum column is stored as a string. `LoreboundDbContext.ConfigureConventions`
  applies this to all enum properties, so new enums (`SettingRole`,
  `CharacterStatus`, ...) need no extra configuration.
- Schema changes ship as EF Core migrations in `Data/Migrations/`; see
  "Migrations" in the root README for commands and rules.
- User emails are unique at the database level (`EmailIndex` on
  `NormalizedEmail`), not only through Identity's app-level check.
- Entities implementing `ITimestamped` get `CreatedAt`/`UpdatedAt` set on save.
  `ExecuteUpdate`/`ExecuteDelete` bypass this, so set `UpdatedAt` yourself.

## DTOs

- Controllers never return entities. Request and response shapes live in
  `Dtos/<Resource>/` as `record` types, e.g. `Dtos/Entries/SettingEntryDto.cs`.
- Entities are mapped with hand-written extension methods in `Mapping/`,
  e.g. `entry.ToDto()`. No AutoMapper.
- On positional request records, put validation attributes on the parameter
  (`record CreateX([Required] string Name)`), not `[property: Required]`;
  MVC rejects the latter with a 500.

## Paging

- List endpoints take `[FromQuery] PageQuery` (`page` defaults to 1,
  `pageSize` defaults to 20 and is capped at 100; out-of-range values are clamped).
- They return `PagedResult<T>`:

  ```json
  { "items": [], "page": 1, "pageSize": 20, "totalCount": 0 }
  ```

## Errors

Every error is RFC 7807 `application/problem+json` with a `traceId` extension:

```json
{ "type": "...", "title": "Not Found", "status": 404, "detail": "Setting not found.", "traceId": "00-..." }
```

- Throw `NotFoundException` (404), `ForbiddenException` (403) or
  `ConflictException` (409) from `Errors/`; `ApiExceptionHandler` maps them.
  Their message becomes `detail`, so write it for API clients.
- Invalid request bodies return 400 `ValidationProblemDetails` with an
  `errors` dictionary keyed by field name.
- Any other exception returns a generic 500 with no exception details.
- Bodyless error statuses (e.g. unmatched routes) also return problem JSON.
  An unmatched route is 404 when signed in but 401 when anonymous, because
  the fallback authorization policy also covers requests with no endpoint.

## CORS

- Origins come from `Cors:AllowedOrigins` (a string array). Development
  allows `http://localhost:3000` via `appsettings.Development.json`; in
  production set `Cors__AllowedOrigins__0` (and `__1`, ...) as environment variables.
- In production the frontend proxies `/api/*` to the API (same origin, see
  `docs/decisions/0001-same-origin-api-proxy.md`), so browsers make no
  cross-origin calls; the origin list still matters because the CSRF check
  only accepts unsafe requests from these origins.
- Credentials are allowed so the auth cookie is sent, so the origin list is
  always explicit, never `*`.
- `AllowAnyHeader()` echoes requested headers (including `X-Requested-With`).
  Do not add `WithHeaders(...)` alongside it: that disables any-header and
  blocks `Content-Type`.

## Authentication

- ASP.NET Core Identity with a **cookie only** (`Auth/AuthenticationSetup.cs`).
  The API never returns bearer or refresh tokens, so never call
  `MapIdentityApi`/`AddIdentityApiEndpoints` (their `/login` can return tokens
  in the body; `AuthenticationTests` fails if those routes appear).
- Cookie `lorebound.auth`: `HttpOnly`, `Secure`, `SameSite=Lax`, 14-day sliding
  expiry when persistent, session cookie otherwise.
- **Secure by default:** a fallback policy requires a signed-in user on every
  endpoint. Mark public actions `[AllowAnonymous]` (minimal endpoints:
  `.AllowAnonymous()`). Today only `/api/health`, the anonymous `/api/auth`
  actions and the Development OpenAPI document are public;
  `FallbackPolicyTests` lists them, so adding one is a deliberate change.
  Never put `[AllowAnonymous]` on a controller class: it overrides
  `[Authorize]` on every action.
- Inject `ICurrentUser` (`UserId`, `Email`, `DisplayName`) to get the signed-in
  user; do not read claims directly. It throws `UnauthorizedAccessException`
  when nobody is signed in. The display name is a cookie claim added by
  `LoreboundClaimsPrincipalFactory` and refreshed on every request.
- Unauthenticated requests to protected endpoints get **401** problem JSON and
  forbidden ones **403**; the API never redirects to a login page.
- The cookie's security stamp is checked against the database on every request
  (Identity's default is every 30 minutes), so a password reset signs the user
  out everywhere at once; logout does the same. Tests on `ApiFactory` (no
  database) turn this off.
- Passwords: at least 10 characters, no forced digit/case/symbol rules. Emails
  are unique. Five failed sign-ins lock an account for 15 minutes.
- `Auth:RequireConfirmedEmail` controls whether sign-in needs a confirmed
  email: `false` in `appsettings.Development.json`, `true` everywhere else
  (and `true` if the key is missing).
- Emails are used as the Identity username, so any valid address is accepted
  (the default username character allowlist is turned off).

### Endpoints

| Endpoint | Success | Failures |
| --- | --- | --- |
| `POST /api/auth/register` `{ email, password, displayName }` | **201** `{ id, email, displayName, emailConfirmed }`; does not sign in. Always emails a confirmation link (`Auth:RequireConfirmedEmail` only decides whether login needs it) | **400** validation problem keyed by `Email`, `Password` or `DisplayName` (1-60 chars, trimmed). When confirmation is required, a taken email gets a generic "Could not register with these details." |
| `POST /api/auth/login` `{ email, password, rememberMe }` | **200** `{ id, email, displayName }` plus `Set-Cookie` | **401** "Invalid email or password." for a wrong password, an unknown email or an unconfirmed email alike; **423** when locked out, with `Retry-After` and `retryAfterSeconds` |
| `POST /api/auth/logout` (signed in) | **204**; signs out **everywhere**: the security stamp changes, so every copy of the cookie (other devices, a stolen copy) stops working, and `Set-Cookie` expires this browser's cookie (same name and path as at sign-in) | **401** when not signed in |
| `POST /api/auth/confirm-email` `{ userId, code }` | **204**; the email is confirmed | **400** "This confirmation link is invalid or has already been used." for an unknown user, a malformed or wrong code, or an already-confirmed account (so a code works once) |
| `POST /api/auth/resend-confirmation` `{ email }` | **204** always; a new link is sent only to an unconfirmed account | **400** only for a missing or malformed email |
| `POST /api/auth/forgot-password` `{ email }` | **204** always; a reset link is sent only to an existing account with a **confirmed** email | **400** only for a missing or malformed email |
| `POST /api/auth/reset-password` `{ email, code, newPassword }` | **204**; the password changes and the user's other sessions end | **400** "This reset link is invalid or has expired." for an unknown email, a malformed, wrong or used code; **400** keyed by `NewPassword` for a weak password |
| `GET /api/users/me` (signed in) | **200** `{ id, email, displayName, emailConfirmed, createdAt }` | **401** when not signed in |
| `PUT /api/users/me` `{ displayName }` (signed in) | **200** with the updated profile; the session stays valid and the display name claim refreshes on the next request | **400** keyed by `DisplayName` (1-60 chars, trimmed); **401** when not signed in. Email and password changes are out of scope for now |

- `rememberMe: false` gives a session cookie; `true` gives the 14-day cookie.
- An unknown email still runs a password hash check, so the response time
  does not reveal which emails are registered.
- Never log passwords or emails; log the user id.

## Email

- Confirmation and reset emails go through `IEmailSender<ApplicationUser>`,
  registered in `Email/EmailSetup.cs`.
- **Development** uses `ConsoleEmailSender`: it logs each link (with the user
  id, not the email) so you can click it from the API terminal. Look for
  `[dev email]`.
- **Everywhere else** uses `UnconfiguredEmailSender`, which throws "No email
  provider is configured" rather than silently dropping mail. To send real
  email, implement `IEmailSender<ApplicationUser>` for the chosen provider,
  read its secrets from environment variables, and register it in
  `EmailSetup` in place of `UnconfiguredEmailSender`.
- Links point at frontend pages, built by `FrontendLinks` from
  `App:FrontendBaseUrl` (`http://localhost:3000` in Development; set
  `App__FrontendBaseUrl` elsewhere):
  - `/confirm-email?userId=<id>&code=<code>`
  - `/reset-password?email=<email>&code=<code>`
- `code` is the Identity token Base64Url-encoded (`EmailCodes.Encode`). An
  endpoint that receives it decodes with `EmailCodes.TryDecode`, which returns
  false for a malformed code (answer 400). It is a single-use link parameter,
  not a session token, so the frontend posts it straight back and never stores it.

## Rate limiting

- Policy `auth` (`Security/RateLimitingSetup.cs`): a fixed window of **10
  requests per minute per client IP**, shared by login, register,
  forgot-password and resend-confirmation (`[EnableRateLimiting(RateLimitingSetup.AuthPolicy)]`).
  Other endpoints are not limited.
- Over the limit: **429** problem JSON with a `Retry-After` header and
  `retryAfterSeconds`. Login lockout is separate and returns 423.
- Limits come from `RateLimiting:Auth:PermitLimit` and `WindowSeconds`. The
  test factory raises the limit because every in-memory client shares one
  partition; `RateLimitTests` sets it back to 10 with `WithConfig`.
- Behind a reverse proxy, the client IP comes from `X-Forwarded-For`, trusted
  only from loopback and the IPs in `ForwardedHeaders:KnownProxies` (set
  `ForwardedHeaders__KnownProxies__0`, ...). Without that, every request
  would appear to come from the proxy and share one limit.

## CSRF

Cookie auth sends the cookie on any request to the API, so unsafe requests
must prove they come from our frontend (`Security/CsrfProtectionMiddleware.cs`).
No CSRF token is used, so nothing secret is ever readable by JavaScript.

- Every `POST`, `PUT`, `PATCH` and `DELETE` must send
  **`X-Requested-With: Lorebound`**. A cross-origin page can only add a custom
  header after a CORS preflight, which the origin allowlist refuses, and an
  HTML form cannot add headers at all.
- If the request has an `Origin` header, it must be in `Cors:AllowedOrigins`
  (case and a trailing slash are ignored). This also blocks other origins on
  the same site, which `SameSite=Lax` alone would let through.
- Violations return **403** problem JSON ("Missing or invalid X-Requested-With
  header." or "Origin not allowed."). `GET`, `HEAD` and `OPTIONS` are not checked.
- The check runs right after CORS, before rate limiting and authentication,
  and applies to anonymous endpoints too (login CSRF).
- The frontend API client (P9-03) sends the header on every request; tests get
  it from the factories; `Lorebound.Api.http` includes it on each unsafe request.
