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
- Access to a setting is a `SettingMembership` row (`GameMaster` or `Player`),
  one per (setting, user), enforced by a unique index. `JoinedAt` defaults to
  the save time. The owner (`CampaignSetting.OwnerUserId`) **always** has a
  `GameMaster` row too, so create both together and never remove or demote the
  owner's row. Deleting a setting deletes its memberships; a user who still
  has memberships cannot be deleted.
- A `SettingInvite` is a 10-character Crockford base32 code (`Sharing/InviteCodes.cs`,
  no `I L O U`), unique across all settings. Accepting one always grants
  `Player`. Codes are stored in plain text so a GM can re-share them; they are
  unguessable and revocable (`RevokedAt`; rows are never deleted while the
  setting exists). Check constraints keep `MaxUses` positive and `UseCount`
  between 0 and `MaxUses`. Deleting a setting deletes its invites; a user who
  created invites cannot be deleted.

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

## Setting access

`ISettingAccess` (`Auth/SettingAccess.cs`) is the one place that decides what
the signed-in user may do in a setting. Every endpoint that reads or changes
setting data calls it **before** touching that data; `SettingAccessConventionTests`
fails if a controller routed under `api/settings` does not inject it.

| Method | Owner | GameMaster | Player | Non-member or missing setting |
| --- | --- | --- | --- | --- |
| `GetRoleAsync(id)` | `GameMaster` | `GameMaster` | `Player` | `null` |
| `RequireMemberAsync(id)` | setting | setting | setting | 404 |
| `RequireGameMasterAsync(id)` | setting | setting | 403 | 404 |
| `RequireOwnerAsync(id)` | setting | 403 | 403 | 404 |
| `VisibleToCurrentUser()` | included | included | included | excluded |

- A non-member gets **404**, not 403, so nobody can probe which setting ids
  exist. A member without the needed role gets **403**.
- The owner always counts as a GameMaster, even if their membership row were
  missing.
- Each check is one database query. The returned setting is tracked, so an
  endpoint can change it and call `SaveChangesAsync`.
- Use `VisibleToCurrentUser()` as the starting point of every settings list
  query, never `db.CampaignSettings` directly.

### Settings endpoints

Who gets what from each `/api/settings` endpoint. `SettingsAuthorizationTests`
and `SettingsListTests` assert every cell, so change them together.

| Endpoint | Anonymous | Non-member | Player | GameMaster | Owner |
| --- | --- | --- | --- | --- | --- |
| `POST /api/settings` | 401 | **201**, becomes owner and GameMaster | n/a | n/a | n/a |
| `GET /api/settings` | 401 | 200, setting not listed | 200, listed | 200, listed | 200, listed |
| `GET /api/settings/{id}` | 401 | 404 | 200 | 200 | 200 |
| `PUT /api/settings/{id}` | 401 | 404 | 403 | 200 | 200 |
| `DELETE /api/settings/{id}` | 401 | 404 | 403 | 403 | **204** |

- An unknown id is 404 for everyone, with the same body a non-member gets.
- `GET /api/settings?role=gm` lists settings where I am owner or GameMaster;
  `role=player` those where I am a Player; any other value is 400.
- Deleting a setting removes its entries, relationships and memberships.

### Invite endpoints

A GameMaster shares a setting by creating an invite code; whoever accepts it
joins as a **Player**. Only GameMasters see or manage codes.
`InviteCreateTests` and `InviteManagementTests` assert this table.

| Endpoint | Anonymous | Non-member | Player | GameMaster | Owner |
| --- | --- | --- | --- | --- | --- |
| `POST /api/settings/{sid}/invites` | 401 | 404 | 403 | **201** | **201** |
| `GET /api/settings/{sid}/invites` | 401 | 404 | 403 | 200 | 200 |
| `DELETE /api/settings/{sid}/invites/{id}` | 401 | 404 | 403 | **204** | **204** |

- Body (optional) `{ expiresInDays?, maxUses? }`: `expiresInDays` 1-90,
  default 7; `maxUses` 1-100, default unlimited. Out of range is 400 keyed by
  the field.
- Response `InviteDto`: `{ id, code, joinUrl, status, expiresAt, maxUses,
  useCount, revokedAt, createdAt }`. `joinUrl` is
  `{App:FrontendBaseUrl}/join/{code}`, built by `FrontendLinks.JoinSetting`.
- `status` is `Active`, `Expired`, `Revoked` or `Exhausted`, computed by
  `InviteRules.StatusAt` (revoked wins over expired, which wins over exhausted).
  `InviteRules.IsActiveAt` is the same rule as a query filter; use these two
  rather than re-deriving validity.
- A setting may have at most **20 active** invites (not revoked, not expired,
  uses left); the 21st is **409**. It is a soft cap: two simultaneous
  creates can both pass it.
- A code collision on insert is retried with a new code (up to 3 attempts).
- `GET` returns `PagedResult<InviteDto>` of every invite (all statuses),
  newest first.
- `DELETE` is a soft revoke: it sets `revokedAt` and keeps the row, so the
  invite stays listed as `Revoked`. Revoking again is a no-op 204 that keeps
  the first `revokedAt`. An invite id from another setting is 404.

### Joining by code

The joining side works by code, not setting id (`InvitesController`, routes
under `api/invites`). `InvitePreviewTests` and `InviteAcceptTests` assert
these rules.

| Endpoint | Anonymous | Any signed-in user, usable code | Unusable code |
| --- | --- | --- | --- |
| `GET /api/invites/{code}` | 401 | 200 `{ settingName, gmDisplayName, alreadyMember }` | 404 |
| `POST /api/invites/{code}/accept` | 401 | 200 `{ settingId, myRole }` | 404 |

- **Every unusable code gets the identical 404** ("Invite not found."):
  unknown, malformed, expired, revoked or used up, for members and
  non-members alike, so a caller cannot tell a dead code from one that never
  existed. Never add a different status or message for one of these cases.
- Codes are read leniently by `InviteCodes.TryNormalize`: case-insensitive,
  hyphens and spaces ignored, `I`/`L` read as `1` and `O` as `0`. Anything
  that still cannot be a code is 404 without a database lookup.
- `gmDisplayName` is the owner's display name. `alreadyMember` is true for
  the owner and any member. Emails are never returned.
- Previewing never changes the invite (`useCount` stays the same).
- Accepting adds a `Player` membership and adds 1 to `useCount`, in one
  transaction. A user who already belongs (owner, GameMaster or Player) gets
  200 with their current role and uses nothing, so retries are safe.
- Race-safe: the use is taken by one `UPDATE ... WHERE` the invite is still
  usable, so parallel accepts of a last use admit exactly one user and the
  rest get 404. A second parallel accept by the same user hits the unique
  membership index before taking a use and is answered as already-member.
  The `CK_SettingInvites_UseCount` check is the last line of defence.
- Rate limited by the `invites` policy (see Rate limiting).

### Members

`SettingMembersController`; `SettingMembersTests` asserts this table.

| Endpoint | Anonymous | Non-member | Player | GameMaster | Owner |
| --- | --- | --- | --- | --- | --- |
| `GET /api/settings/{sid}/members` | 401 | 404 | 200 | 200 | 200 |
| `PATCH /api/settings/{sid}/members/{userId}` `{ role }` | 401 | 404 | 403 | 403 | **200** |

- `GET` returns `PagedResult<MemberDto>`, each `{ userId, displayName, role,
  isOwner, joinedAt }`: the owner first, then GameMasters, then Players,
  each oldest first. **Emails are never exposed.**
- Only the **owner** changes roles, so there are no co-owners. Other
  GameMasters manage invites and remove Players (P3-07) but cannot promote
  or demote.
- `PATCH` returns the updated `MemberDto`. Setting the role a member already
  has is a no-op 200. The owner cannot be demoted (**409**); a `userId` that
  is not a member of this setting is 404; a missing or unknown role
  (`GameMaster` and `Player` only) is 400.

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
- Policy `invites`: the same limit with its **own counter**, on the
  endpoints that look up an invite code (preview and accept), so
  guessing codes is slow and does not spend the caller's login attempts.
- Other endpoints are not limited.
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
