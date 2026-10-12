# P8-07 security review

- **Date:** 2026-10-12
- **Issue:** #124 (P8-07)
- **Scope:** the API before the frontend goes live on real data, deployed as
  decided in [ADR 0003](../decisions/0003-production-hosting.md): the Next.js
  frontend on Vercel, the API on Render, PostgreSQL on Supabase. The browser
  only talks to the Vercel host, which forwards `/api/*` to Render
  ([ADR 0001](../decisions/0001-same-origin-api-proxy.md)).

Every item on the checklist is either resolved in the P8-07 PR, already
covered by an earlier task (with the test that proves it), or filed as an
issue.

## Summary

| Area | Result |
| --- | --- |
| Cookie flags in Production | OK, now tested on a Production host |
| HSTS | **Fixed:** was missing; sent on every response outside Development |
| `AllowedHosts` | Config on Render (P8-11); filtering tested; checklist item |
| CORS allowlist and CSRF | OK; re-checked against the Vercel origin and preview origins |
| Authorization test per controller | OK; **new test** covers every protected endpoint |
| Rate limits | OK on auth and invite endpoints; two token endpoints left unlimited on purpose |
| .NET dependencies | No known vulnerabilities |
| npm dependencies | **Fixed:** critical `next` advisory and others; dev-tool advisory filed as #229 |
| Secrets in the repo | None found (gitleaks, full history) |
| Hosting checklist (Supabase Data API, secrets) | Added to P8-11 (#228) |

## Findings

### 1. Cookie flags: OK

`lorebound.auth` is `HttpOnly`, `Secure` (always, whatever the request
scheme), `SameSite=Lax`, `Path=/` and has no `Domain`, so it belongs only to
the host the browser sees: the Vercel host. Because `Secure` does not depend
on the request scheme, it stays set behind Render's proxy, where the API sees
plain HTTP. A browser calling the public Render URL directly has no cookie
for that host.

Tests: `AuthenticationTests` (Development) and, new,
`ProductionHardeningTests.Production_auth_cookie_is_secure_httponly_lax_and_host_only`.

### 2. HSTS: fixed

The API sent no `Strict-Transport-Security` header. ASP.NET Core's `UseHsts`
would not have helped: it only adds the header to requests that arrive over
HTTPS. On Render, TLS ends at Render's proxy, and the API does not trust
`X-Forwarded-Proto`, because the proxies' IPs cannot be pinned (ADR 0003).

`Security/StrictTransportSecurityMiddleware` now sends
`Strict-Transport-Security: max-age=31536000` on every response outside
Development, error responses included. That is safe: browsers ignore the
header on a response that did not come over HTTPS (RFC 6797, section 8.1).
Vercel passes the API's response headers through, so the header reaches the
browser on the Vercel host.

`UseHttpsRedirection` stays in place. On Render it does nothing, because no
HTTPS port is known inside the container; Render's own proxy redirects HTTP
to HTTPS. Confirm that during P8-11 (see the checklist).

Tests: `ProductionHardeningTests.Production_responses_carry_HSTS_even_errors`
and `Development_sends_no_HSTS`.

### 3. `AllowedHosts`: configuration, verified at deploy

`appsettings.json` keeps `"AllowedHosts": "*"` so local runs and the Docker
image work anywhere. On Render, `AllowedHosts` is set to the Render host
(ADR 0003), so requests for any other `Host` get 400.

The risk this guards against is low: the API never builds URLs from the
request's `Host`. Links in emails and invites use `App:FrontendBaseUrl`. It
is still worth setting, but two things must be checked when it is first
deployed, because a wrong value takes the site down:

- the `Host` header that Vercel's rewrite sends to Render;
- the `Host` header Render's health check sends to `/api/health`.

Test: `ProductionHardeningTests.Allowed_hosts_refuses_other_host_headers`
(the filter works once configured).

### 4. CORS and CSRF: OK, re-checked for Vercel

The rules from P1-10 and P1-11 hold:

- Every unsafe request needs `X-Requested-With: Lorebound`.
- An unsafe request that has an `Origin` must come from `Cors:AllowedOrigins`.
- CORS echoes headers only for allowlisted origins, with credentials, never
  `*`.

New in this review: a Production host configured with only the Vercel
production origin accepts a write from that origin. It refuses writes from:

- a Vercel preview origin (expected, ADR 0003);
- the Render origin;
- the `http://` variant of the Vercel origin.

Tests: `CsrfTests`, `CorsTests`, and, new,
`ProductionHardeningTests.Production_writes_are_accepted_only_from_the_vercel_origin`.

### 5. Authorization tests for every controller: OK, now enforced

Every controller already had authorization tests. Settings, entries,
relationships, members, invites and characters have full matrices (anonymous
401, non-member 404, Player 403 on writes, GM or owner success). The
user-scoped controllers (dashboard, builder steps, relationship types, users,
invites by code) test 401, since they have no other roles.

What was missing was a guard for the future, so a new controller can't ship
without these tests. Two kinds of test now provide it:

- **New:**
  `FallbackPolicyTests.Every_protected_api_endpoint_returns_401_to_anonymous_callers`
  calls all 39 non-anonymous API endpoints without a cookie and expects 401
  from each.
- **Existing:**
  - `FallbackPolicyTests.Only_the_intended_api_endpoints_are_anonymous` pins
    the list of public endpoints.
  - `SettingAccessConventionTests` and `CharacterAccessConventionTests`
    require every setting or character controller to authorize through
    `ISettingAccess` or `ICharacterAccess`, which give the 404 and 403 parts
    of the matrix.

### 6. Rate limits: OK

| Endpoint | Policy |
| --- | --- |
| login, register, forgot-password, resend-confirmation, `DELETE /api/users/me` | `auth`, 10 per minute per client IP |
| invite preview and accept (`/api/invites/{code}`) | `invites`, same limit, own counter |

`confirm-email` and `reset-password` are not rate limited. That is
deliberate: they take Identity tokens protected by Data Protection, which
cannot be guessed, and each token is single-use. Lockout (5 failed logins,
15 minutes) applies on top of the login limit.

Behind Vercel and Render the client IP is not reliable, so all users may
share one counter. ADR 0003 handles this through configuration in P8-11,
which checks the partition key on the deployed API.

Tests: `RateLimitTests`, `InvitePreviewTests`, `InviteAcceptTests`.

### 7. Dependency audit

**.NET:** `dotnet list Lorebound.slnx package --vulnerable
--include-transitive` reports no vulnerable packages in `Lorebound.Api` or
`Lorebound.Api.Tests`.

**npm (`frontend/`):** `npm audit` reported 16 findings: 2 critical, 12 high
and 2 moderate. Fixed in this PR:

- `next` 16.3.4 → 16.4.0 (critical: remote code execution in `next/og`
  `ImageResponse`). The app doesn't use `next/og`, but `next` is the
  production server, so it is upgraded. `eslint-config-next` moves to 16.4.0
  with it.
- `npm audit fix` (no breaking changes) for:
  - `proxy-addr` (critical);
  - `sharp`, `source-map-js`, `brace-expansion`, `@modelcontextprotocol/sdk`
    (high);
  - `fast-uri`, `ip-address` (moderate).

**Left:** 9 high findings from one advisory in `braces` (GHSA-vfj7-8cjw-p6xm).
They reach the app only through the `shadcn` CLI and the ESLint plugin, which
are build tools that expand glob patterns from this repo. No fixed `braces`
release exists, and npm's suggested fixes are downgrades. Filed as **#229**.

### 8. Secrets: none found

[gitleaks](https://github.com/gitleaks/gitleaks) v8.24.3 scanned all 130
non-merge commits on every ref (`--log-opts=--all`): no leaks.

- Tracked config holds no secrets:
  - `appsettings*.json` have no connection string or password;
  - `docker-compose.yml` reads `POSTGRES_PASSWORD` from `.env`;
  - only `.env.example` is tracked.
- `.env`, `.env.local` and `.env.*.local` are ignored. The local connection
  string lives in user-secrets.
- The seed accounts' passwords (`lorebound-gm-dev`, `lorebound-player-dev`)
  are documented on purpose. The seeder refuses to run outside Development
  (P8-02, `DevelopmentSeederTests`).

To repeat the scan (Docker):

```powershell
docker run --rm -v "${PWD}:/repo" zricethezav/gitleaks:v8.24.3 git /repo --redact --log-opts="--all"
```

### 9. Hosting checklist: added to P8-11

ADR 0003 makes the API public and hosts the database on Supabase. These
items are added to P8-11 (#228) so the deploy runbook (`docs/deployment.md`)
includes them:

1. Supabase **Data API turned off** (or `public` removed from its exposed
   schemas). Check that the anon key cannot read `AspNetUsers`.
2. Secrets only in the hosts' settings: the connection string on Render,
   never in the repo, `.env.example` or the runbook.
3. `ASPNETCORE_ENVIRONMENT=Production` on Render. That turns off the OpenAPI
   document, `/scalar` and seeding, and turns on HSTS. Check that
   `/openapi/v1.json` is not served (anonymous callers get 401, like any
   unknown route).
4. `Cors__AllowedOrigins__0` set to the Vercel **production** origin only.
5. `AllowedHosts` set to the Render host. Afterwards, check that Render's
   health check stays green and that login through the Vercel URL works
   (item 3 above).
6. Response headers through the Vercel URL include
   `Strict-Transport-Security`. `http://` to the Render URL redirects to
   `https://`.
7. The auth cookie on the Vercel host is `Secure`, `HttpOnly`, `SameSite=Lax`
   and has no `Domain`.
8. Rate-limit partition checked (ADR 0003).

## Also confirmed (earlier tasks)

These earlier protections were confirmed again and are not repeated here:

- **Problem responses:** they never include exception details outside
  Development (`ProblemDetailsTests`).
- **Logs (P8-05):** no passwords, cookies, invite codes or full emails;
  raw paths are not logged (`RequestLoggingTests`).
- **OpenAPI document and `/scalar`:** Development only
  (`OpenApiDocumentTests`).
- **Session revocation:** the security stamp is checked on every request, so
  password reset, logout and account deletion end other sessions.
- **GM-only entries:** hidden from Players everywhere, including the
  dashboard (P4, P8-01).
