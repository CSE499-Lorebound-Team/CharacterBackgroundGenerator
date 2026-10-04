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

## CORS

- Origins come from `Cors:AllowedOrigins` (a string array). Development
  allows `http://localhost:3000` via `appsettings.Development.json`; in
  production set `Cors__AllowedOrigins__0` (and `__1`, ...) as environment variables.
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
- Unauthenticated requests to protected endpoints get **401** problem JSON and
  forbidden ones **403**; the API never redirects to a login page.
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
| `POST /api/auth/register` `{ email, password, displayName }` | **201** `{ id, email, displayName, emailConfirmed }`; does not sign in | **400** validation problem keyed by `Email`, `Password` or `DisplayName` (1-60 chars, trimmed). When confirmation is required, a taken email gets a generic "Could not register with these details." |
| `POST /api/auth/login` `{ email, password, rememberMe }` | **200** `{ id, email, displayName }` plus `Set-Cookie` | **401** "Invalid email or password." for a wrong password, an unknown email or an unconfirmed email alike; **423** when locked out, with `Retry-After` and `retryAfterSeconds` |
| `POST /api/auth/logout` (signed in) | **204**; `Set-Cookie` expires the auth cookie (same name and path as at sign-in) | **401** when not signed in |

- `rememberMe: false` gives a session cookie; `true` gives the 14-day cookie.
- An unknown email still runs a password hash check, so the response time
  does not reveal which emails are registered.
- Never log passwords or emails; log the user id.
