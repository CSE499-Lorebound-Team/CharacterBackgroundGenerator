@@@ P8-01
title: GET /api/dashboard aggregate
type: feature
area: api
phase: 8
priority: P1
size: M
depends: P2-04, P6-04
@@@
## Summary
One request for the dashboard page.

## Tasks
- [ ] Return `{ counts: { settingsAsGm, settingsAsPlayer, charactersDraft, charactersComplete }, recentSettings (5), recentCharacters (5), recentActivity (10) }`.
- [ ] `counts`: settings the caller owns or is a GameMaster of (the owner counts even without a membership row), settings where the caller is a Player, and the caller's own characters by status.
- [ ] `recentSettings` and `recentCharacters` are the 5 most recently updated, with the same item shape as `GET /api/settings` and `GET /api/characters`.
- [ ] `recentActivity` is derived from `UpdatedAt` across the caller's settings, visible entries, and characters (no activity-log table).
- [ ] Activity items: `{ kind, text, at, id, settingId }`. `kind` is `Setting`, `Entry` or `Character`; `id` is that item and `settingId` its setting, so the dashboard can link to it.
- [ ] Activity text names what changed, not who changed it (nothing records who): "Created Osepia" / "Edited Osepia", "Added Sasymon to Osepia" / "Edited Sasymon", "Started Theron Vale" / "Updated Theron Vale". The first form applies while `UpdatedAt` equals `CreatedAt`.
- [ ] Respect visibility rules (no hidden entries for players).

## Implementation details
- Use three small `AsNoTracking` queries; union in memory; avoid N+1.

## Acceptance criteria
- The response matches this spec and the Dashboard section of `api/README.md`.
- The API sets the list sizes (5, 5, 10). The current dashboard page (3 cards, mock data) is a draft; it adapts to this contract in P9-15 unless the team reviews and changes the spec.

@@@ P8-02
title: Development seed data
type: task
area: devex
phase: 8
priority: P2
size: S
depends: P5-03, P6-03
@@@
## Summary
A fresh clone should look like the wireframes.

## Tasks
- [ ] Dev-only seeder behind `--seed` (`dotnet run -- --seed`, which seeds and then keeps running) or `Seed:Enabled` config.
- [ ] Users: a GM and a Player (documented dev passwords, never in Production): `gm@lorebound.local` / `lorebound-gm-dev` (Joseph Marlow) and `player@lorebound.local` / `lorebound-player-dev` (Lyra Holt).
- [ ] Setting "Osepia" with entries (Sasymon, Ymenite Region, Northern Marches, Nigallu, Merchant Guild, Cult of Beléna), at least one GM-only entry, and relationships. Also River Cities (culture), Caravan Guard and Scholar (professions), so the builder has a culture to narrow to and professions to pick. Cult of Beléna is the GM-only entry.
- [ ] Player membership and a draft character (Theron Vale), with Sasymon as homeland, on the culture step.
- [ ] Idempotent (safe to run twice): rows are found by natural key and only missing ones are added; existing rows, passwords included, are never changed.
- [ ] With seeding on outside Development (Production, Staging, ...), the API refuses to start.

## Acceptance criteria
- After seeding, both users can log in and see role-appropriate data.
- The users, passwords and seeded contents are documented in the root README ("Seed data").

@@@ P8-03
title: OpenAPI polish and API explorer in Development
type: task
area: docs
phase: 8
priority: P2
size: S
depends: P7-05
@@@
## Summary
Make the API self-documenting.

## Tasks
- [ ] `ProducesResponseType` on all actions for their success status and domain errors (200/201/204, 400, 403, 404, 409).
- [ ] The cross-cutting statuses are derived from endpoint metadata by an OpenAPI transformer rather than repeated on every action, so they cannot drift: 401 plus the cookie security scheme on every non-`[AllowAnonymous]` action, the required `X-Requested-With` header and its 403 on every POST/PUT/PATCH/DELETE, and 429 on every rate-limited action.
- [ ] Bodies are documented as `application/json` only, and errors as `application/problem+json` (400 with validation `errors`).
- [ ] XML doc comments surfaced as operation descriptions (every action has a summary).
- [ ] Add an explorer UI in Development only: Scalar at `/scalar`, against the built-in OpenAPI document at `/openapi/v1.json`. Neither is served outside Development.
- [ ] Document the cookie auth flow and the `X-Requested-With` requirement in the OpenAPI description.

## Acceptance criteria
- Every endpoint appears with request/response schemas, enforced by `Conventions/OpenApiDocumentTests`.

@@@ P8-04
title: Query performance and index review
type: task
area: database
phase: 8
priority: P2
size: S
depends: P8-01
@@@
## Summary
Verify list endpoints scale before more data exists.

## Tasks
- [ ] Log generated SQL for list/dashboard endpoints in Development (EF Core logs every command at Information with the request's `TraceId`).
- [ ] Ensure `AsNoTracking` and projection everywhere; no N+1 or cartesian explosion. Enforced by `Performance/QueryBudgetTests`: each read endpoint's SQL command count is the same with 1 and 12 rows and within a pinned ceiling.
- [ ] Run `EXPLAIN ANALYZE` on entry search and dashboard with seeded volume (thousands of entries). Done at ~80,000 entries including 500 other tenants' settings, with `docs/performance/p8-04-volume.sql` and `p8-04-explain.sql`.
- [ ] Add missing indexes via migration (`AddListIndexes`: entries by setting and name, entries by setting and `UpdatedAt`, characters by owner and `UpdatedAt`); consider `pg_trgm` for `ILIKE` search if slow. Decided not yet: search is 8.5 ms at 10,000 entries per setting, and `Name` is `citext`, so trigram indexing needs an expression index and a query change. Revisit past ~50,000 entries in one setting or a 50 ms search p95.
- [ ] Fix what the review finds: the dashboard's recent-entries activity sorted the whole entries table (cost followed the whole database), so it now takes the newest entries per setting the caller belongs to; settings detail no longer runs its access check twice.

## Acceptance criteria
- Findings recorded in `docs/performance/p8-04-query-review.md`; no list endpoint runs more than a handful of queries (4-5 including the 2 every signed-in request makes; the dashboard 10).

@@@ P8-05
title: Health checks and structured logging
type: task
area: api
phase: 8
priority: P2
size: S
depends: P0-08
@@@
## Summary
Operational visibility.

## Tasks
- [ ] Extend `/api/health` with a DB check (`AddDbContextCheck`), keeping the current JSON shape. The existing `database` field (P0-08) keeps its values: `"connected"` (200) or `"unreachable"` (503). Decided on review instead of the originally proposed `"up|down"`, so the delivered contract and its docs do not change.
- [ ] Request logging with correlation id (`traceId`) that matches ProblemDetails: one line per request with method, **route template** (never the raw path or query string), status and duration, and a `TraceId` scope on every log line of the request.
- [ ] Structured output: stdout only, one JSON object per line outside Development; readable text with scopes in Development.
- [ ] Never log passwords, cookies, invite codes, or full emails. ASP.NET Core's hosting request logger (raw path in its scope) is off. The only exception is the Development-only console email sender, which is the local mailbox and logs the emailed links.

## Acceptance criteria
- Stopping Postgres makes health report database down (HTTP 503, `"database": "unreachable"`).
- A test proves no password, cookie, invite code or email reaches the logs, and that the `TraceId` scope equals the problem `traceId`.

@@@ P8-06
title: Final README and API documentation pass
type: docs
area: docs
phase: 8
priority: P2
size: S
depends: P8-03
@@@
## Summary
Documentation matches the finished API.

## Tasks
- [ ] README: DB setup, migrations, seed data, auth (cookie-only) and CSRF header, sharing model, permission matrix.
- [ ] `docs/api/` endpoint reference generated or copied from OpenAPI: generated (`docs/api/README.md` plus `openapi.json`) by `Conventions/ApiReferenceTests`, which fails when the committed copy is stale; `UPDATE_API_DOCS=1` regenerates it.
- [ ] Update the repository tree (Services/Dtos/Tests). The API has no Services folder: controllers call helpers in `Auth/`, `Data/` and `Sharing/`, and the tree says so.
- [ ] Link the project board.
- [ ] Fix what writing the reference exposed in the OpenAPI document: enums were documented as integers (the API sends names), and paged lists advertised a `skip` query parameter that is never bound.

## Acceptance criteria
- A new teammate can run, seed, and log in using only the docs: the root README's Quick Start goes from clone to signed in (through the `/scalar` explorer until Phase 9 adds sign-in pages).

@@@ P8-07
title: Security review pass
type: task
area: security
phase: 8
priority: P1
size: S
depends: P8-04
@@@
## Summary
Checklist review before frontend goes live on real data.

## Tasks
- [ ] Cookie flags in Production; HSTS enabled; `AllowedHosts` restricted (the Render host, per ADR 0003).
  - HSTS (`max-age=31536000`) is sent on every response outside Development, by `Security/StrictTransportSecurityMiddleware` rather than `UseHsts`. `UseHsts` only marks HTTPS requests, and behind Render the API sees plain HTTP: `X-Forwarded-Proto` is not trusted (ADR 0003).
  - `AllowedHosts` stays `*` in `appsettings.json` and is set to the Render host in Render's settings (P8-11). It must be checked against the `Host` sent by Vercel's rewrite and by Render's health check.
- [ ] CORS allowlist and CSRF checks re-verified, including against the Vercel origin (ADR 0003). A Production host accepts writes from the Vercel production origin only, not from preview origins.
- [ ] Hosting checklist from ADR 0003 written into the P8-11 runbook: Supabase Data API off, secrets only in host settings. Added to P8-11's tasks as its security checklist.
- [ ] Every controller has an authorization test. Instead of a one-off grep, `FallbackPolicyTests` calls every non-anonymous API endpoint without a cookie and expects 401. The setting and character access conventions cover the 404/403 part.
- [ ] Rate limits on invite preview/accept and auth endpoints. `confirm-email` and `reset-password` stay unlimited: their tokens cannot be guessed and are single-use.
- [ ] Dependency audit (`dotnet list package --vulnerable`, `npm audit`).
  - .NET is clean.
  - npm: `next` was upgraded to 16.4.0 (critical advisory), and `npm audit fix` fixed the rest that has a fix.
  - The dev-tool-only `braces` advisory is filed as #229.
- [ ] No secrets in repo; verify with a secret scan. gitleaks over the full history found none.

## Acceptance criteria
- Findings resolved or filed as issues, and recorded in `docs/security/p8-07-security-review.md`.
- `ProductionHardeningTests` covers HSTS, the Production cookie flags, the Vercel origin and `AllowedHosts` filtering.

@@@ P8-08
title: Persist Data Protection keys in Postgres
type: task
area: security
phase: 8
priority: P1
size: S
depends: P0-08
@@@
## Summary
The auth cookie and the email-confirmation and password-reset tokens are protected with Data Protection keys. Render's free plan restarts the API with an empty filesystem, so keys kept in memory or on disk are lost: everyone is signed out and emailed links stop working (ADR 0003).

## Tasks
- [ ] `PersistKeysToDbContext<LoreboundDbContext>` (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`); the DbContext implements `IDataProtectionKeyContext`.
- [ ] Migration adding the `DataProtectionKeys` table.
- [ ] `SetApplicationName("Lorebound")`, so keys stay valid when the container or path changes.
- [ ] api/README: where the keys live and that they must never be deleted while users are signed in.
  - It also says the keys are stored as plain XML (no key encryptor), so the database credentials are the real protection.
- [ ] Tests:
  - The test database reset keeps `DataProtectionKeys`, as production does.
  - `ApiFactory` (no database) uses in-memory keys.

## Acceptance criteria
- A test signs in, builds a new app host on the same database, and the old cookie still authenticates (`Auth/DataProtectionKeysTests`).
- A password-reset link issued by one host also works on the new host.

@@@ P8-09
title: Turn email off for the demo
type: task
area: auth
phase: 8
priority: P1
size: S
depends: P1-05
@@@
## Summary
Registration always sends a confirmation email, and outside Development `UnconfiguredEmailSender` throws, so sign-up on the deployed API returns 500 after creating the user. The team decided the demo sends no email (ADR 0003, "Email").

## Tasks
- [ ] A sender that sends nothing (`DisabledEmailSender`), registered outside Development only when `Email:Provider` is `None`. It never logs links or codes.
- [ ] `EmailSetup` picks the sender from `Email:Provider`; without it, Production keeps `UnconfiguredEmailSender` (fail loudly). Development keeps `ConsoleEmailSender`.
- [ ] Render sets `Email__Provider=None` and `Auth__RequireConfirmedEmail=false` (P8-11).
- [ ] api/README "Email" section: the provider setting, and that confirm-email and forgot-password send nothing in the demo.

## Acceptance criteria
- With `ASPNETCORE_ENVIRONMENT=Production`, `Email:Provider=None` and `Auth:RequireConfirmedEmail=false`: register returns 201, login succeeds without confirming, and resend-confirmation and forgot-password return 204.
- Production without `Email:Provider` still uses `UnconfiguredEmailSender`.
- A test proves no link or code reaches the logs.

@@@ P8-10
title: API Dockerfile for Render
type: chore
area: devex
phase: 8
priority: P1
size: S
depends: P0-08
@@@
## Summary
Render has no native .NET runtime, so the API deploys as a Docker web service built from `api/` (ADR 0003).

## Tasks
- [ ] `api/Dockerfile`: multi-stage build (.NET 10 SDK, then the ASP.NET runtime image), running as the image's non-root user and listening on 8080.
- [ ] `api/.dockerignore` (`bin/`, `obj/`, local settings and secrets).
- [ ] CI builds the image on PRs so a broken Dockerfile fails before deploy.
- [ ] Root README: how to build and run the image locally against the docker-compose database.

## Acceptance criteria
- `docker build` succeeds, and the container serves `GET /api/health` with 200 against a reachable database.

@@@ P8-11
title: Deploy the course demo (Vercel, Render, Supabase)
type: task
area: devex
phase: 8
priority: P1
size: M
depends: P8-08, P8-09, P8-10
@@@
## Summary
Stand up the demo environment chosen in ADR 0003 and write down how to repeat it. Blocked until the team decides who owns the Vercel, Render and Supabase accounts (ADR 0003, open question 1).

## Tasks
- [ ] Supabase project; Data API turned off; session pooler connection string with `SSL Mode=Require`.
- [ ] Apply migrations from a developer machine (`dotnet ef database update` with the Supabase connection string). The app never migrates at startup.
- [ ] Render Docker web service from `api/`, health check `/api/health`, environment variables from ADR 0003.
- [ ] Vercel project with root directory `frontend/` and `API_ORIGIN` set to the Render URL (the rewrite is already in `next.config.ts`).
- [ ] Check the rate-limit partition key on the deployed API; if all clients share one IP, raise `RateLimiting__Auth__PermitLimit` (ADR 0003).
- [ ] Security checklist from P8-07 (`docs/security/p8-07-security-review.md`, section 9) in the runbook and checked on the deployed app:
  - Supabase Data API off: the anon key cannot read `AspNetUsers`.
  - Secrets only in host settings.
  - `ASPNETCORE_ENVIRONMENT=Production`: `/openapi/v1.json` and `/scalar` are not served.
  - `Cors__AllowedOrigins__0` is the Vercel production origin only.
  - `AllowedHosts` is the Render host, while the health check stays green and login through Vercel works.
  - `Strict-Transport-Security` reaches the browser, and `http://` redirects to `https://`.
  - The auth cookie is `Secure`, `HttpOnly`, `SameSite=Lax` and has no `Domain`.
- [ ] `docs/deployment.md` runbook: accounts and owners, every variable (no secret values), migration steps, redeploy, and demo-day checks (wake Render, check Supabase is not paused).

## Acceptance criteria
- From the public Vercel URL: register, log in, log out and create a setting succeed; the auth cookie belongs to the Vercel host.
- The rest of the app (characters, builder) is re-checked from the same URL once Phase 9 lands.
