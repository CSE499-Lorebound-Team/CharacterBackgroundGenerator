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
- [ ] Log generated SQL for list/dashboard endpoints in Development.
- [ ] Ensure `AsNoTracking` and projection everywhere; no N+1 or cartesian explosion.
- [ ] Run `EXPLAIN ANALYZE` on entry search and dashboard with seeded volume (thousands of entries).
- [ ] Add missing indexes via migration; consider `pg_trgm` for `ILIKE` search if slow.

## Acceptance criteria
- Findings recorded; no list endpoint runs more than a handful of queries.

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
- [ ] `docs/api/` endpoint reference generated or copied from OpenAPI.
- [ ] Update the repository tree (Services/Dtos/Tests).
- [ ] Link the project board.

## Acceptance criteria
- A new teammate can run, seed, and log in using only the docs.

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
- [ ] Cookie flags in Production; HSTS enabled; `AllowedHosts` restricted.
- [ ] CORS allowlist and CSRF checks re-verified.
- [ ] Every controller has an authorization test (grep for controllers with no matrix test).
- [ ] Rate limits on invite preview/accept and auth endpoints.
- [ ] Dependency audit (`dotnet list package --vulnerable`, `npm audit`).
- [ ] No secrets in repo; verify with a secret scan.

## Acceptance criteria
- Findings resolved or filed as issues.
