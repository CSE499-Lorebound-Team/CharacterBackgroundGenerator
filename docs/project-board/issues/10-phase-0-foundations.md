@@@ P0-01
title: Remove weatherforecast scaffold and tidy Program.cs
type: chore
area: api
phase: 0
priority: P1
size: XS
depends:
@@@
## Summary
`api/Program.cs` still contains the default `/weatherforecast` minimal endpoint and `WeatherForecast` record from the template.

## Tasks
- [ ] Delete the `summaries` array, `MapGet("/weatherforecast")`, and the `WeatherForecast` record.
- [ ] Reorder the pipeline: exception handler, HTTPS redirection, CORS, then `MapControllers`.
- [ ] Remove any weatherforecast requests from `Lorebound.Api.http`.

## Implementation details
- Leave a clearly commented placeholder where `UseAuthentication`/`UseAuthorization` will go (added in P1-01).
- `HealthController` stays as-is.

## Acceptance criteria
- `GET /api/health` still returns `{ status: "healthy", application: "Lorebound API" }`.
- `/weatherforecast` returns 404.

@@@ P0-02
title: Convert User to ASP.NET Core Identity user
type: task
area: database
phase: 0
priority: P0
size: M
depends:
@@@
## Summary
Auth is in scope, so `User` must become an Identity user. Do this **before** the first migration exists so we ship one clean schema.

## Tasks
- [ ] Add package `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (10.0.x, matching EF Core).
- [ ] Replace `User` with `ApplicationUser : IdentityUser<Guid>` keeping `DisplayName` (required, max 60) and adding `CreatedAt`.
- [ ] Change `LoreboundDbContext` to `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`; keep `base.OnModelCreating(modelBuilder)` as the first line.
- [ ] Update `CampaignSetting.Owner` and the `User`/`Users` references to `ApplicationUser`.
- [ ] Remove the old `Users` DbSet (provided by Identity).

## Implementation details
- The Identity roles tables stay unused because roles are per-setting (see P2-01). Do not add global roles.
- Keep `Email` from `IdentityUser` (normalized, unique index is provided by Identity).
- `ApplicationUser.CampaignSettings` navigation stays; FK behavior for `OwnerUserId` becomes `Restrict` (a user with owned settings cannot be hard-deleted; see P6-11).

## Acceptance criteria
- Solution builds with no reference to the old `User` class.
- Model snapshot contains Identity tables plus existing domain tables.

@@@ P0-03
title: Add CreatedAt/UpdatedAt timestamps to all entities
type: task
area: database
phase: 0
priority: P1
size: S
depends: P0-02
@@@
## Summary
The UI shows "Updated 2 days ago" and the dashboard sorts by recency, but no entity has timestamps.

## Tasks
- [ ] Add `ITimestamped { DateTimeOffset CreatedAt; DateTimeOffset UpdatedAt; }`.
- [ ] Implement on `CampaignSetting`, `SettingEntry`, `SettingEntryRelationship` (and `ApplicationUser.CreatedAt`).
- [ ] Override `SaveChangesAsync` (and `SaveChanges`) to set `CreatedAt` on Added and `UpdatedAt` on Added/Modified.
- [ ] Use `TimeProvider` (register `TimeProvider.System`) so tests can control time.

## Implementation details
- Store as `timestamptz` (`DateTimeOffset` maps correctly in Npgsql; use UTC only).
- `ExecuteUpdate`/`ExecuteDelete` bypass this hook; any bulk update must set `UpdatedAt` explicitly.

## Acceptance criteria
- Creating then updating an entity yields `CreatedAt < UpdatedAt`.
- Unit test with a fake `TimeProvider` proves both fields.

@@@ P0-04
title: Establish API conventions - string enums, DTOs, paging
type: task
area: api
phase: 0
priority: P0
size: S
depends:
@@@
## Summary
Set shared conventions once so every later endpoint is consistent.

## Tasks
- [ ] Configure `JsonStringEnumConverter` globally so the frontend sends/receives `"Location"` not `0`.
- [ ] Store every enum column as a string via `HasConversion<string>()` (`SettingEntryType`, later `SettingRole`, `CharacterStatus`).
- [ ] Create `api/Dtos/` (records only) with folders per resource; entities are never returned from controllers.
- [ ] Add `PagedResult<T>(Items, Page, PageSize, TotalCount)` and `PageQuery` (`page` default 1, `pageSize` default 20, max 100).
- [ ] Use hand-written mapping extension methods (`ToDto()`); no AutoMapper.

## Implementation details
- camelCase JSON is the ASP.NET default; keep it.
- String enums must be set **before** P0-08 so the first migration has string columns.

## Acceptance criteria
- Conventions written in `api/README` section or XML comments on the base types.
- A sample DTO round-trips an enum as its name.

@@@ P0-05
title: Global error handling with ProblemDetails
type: task
area: api
phase: 0
priority: P1
size: S
depends:
@@@
## Summary
All errors should be RFC 7807 `application/problem+json` with a stable shape the frontend can parse.

## Tasks
- [ ] `AddProblemDetails()` and `UseExceptionHandler()`; never leak stack traces outside Development.
- [ ] Add small exceptions (`NotFoundException`, `ForbiddenException`, `ConflictException`) mapped to 404/403/409 by an `IExceptionHandler`.
- [ ] Ensure model-validation failures return `ValidationProblemDetails` (400) with field errors.
- [ ] Add a `traceId` extension to every problem response.

## Implementation details
- Use `IExceptionHandler` (built into ASP.NET Core) registered with `AddExceptionHandler<T>()`.
- 401/403 from the auth pipeline must also be JSON (see P1-01).

## Acceptance criteria
- Throwing `NotFoundException` in a test controller returns 404 problem JSON with `traceId`.
- Invalid body returns 400 with an `errors` dictionary.

@@@ P0-06
title: Database configuration, user-secrets and local Postgres compose file
type: task
area: devex
phase: 0
priority: P0
size: S
depends:
@@@
## Summary
No connection string exists anywhere (`GetConnectionString("DefaultConnection")` is null), so any DB call fails. README promises DB setup docs "once finalized".

## Tasks
- [ ] Add `docker-compose.yml` at repo root with a Postgres 17 service (named volume, port 5432, dev-only credentials via `.env`, not committed).
- [ ] Document `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."` in the README.
- [ ] Fail fast at startup if the connection string is missing, with a clear message.
- [ ] Add `.env.example` for compose variables; ensure `.env` is git-ignored (it already is).

## Implementation details
- Do not put credentials in `appsettings*.json`.
- `dotnet ef` tool: document `dotnet tool install --global dotnet-ef`.

## Acceptance criteria
- A new developer can run `docker compose up -d` and `dotnet run` and reach a working DB connection following only the README.

@@@ P0-07
title: Config-driven CORS with credentials
type: task
area: security
phase: 0
priority: P0
size: XS
depends:
@@@
## Summary
Cookie auth requires the browser to send credentials cross-origin; CORS must allow credentials and use an explicit origin allowlist (never `*`).

## Tasks
- [ ] Read `Cors:AllowedOrigins` (string array) from configuration; default `["http://localhost:3000"]` in Development.
- [ ] Add `.AllowCredentials()`; keep `AllowAnyHeader/AnyMethod` for now.
- [ ] Expose `X-Requested-With` as an allowed header explicitly (needed by P1-10).

## Implementation details
- `AllowCredentials` throws at startup if combined with `AllowAnyOrigin`; that is intended.
- Production origin comes from environment variable `Cors__AllowedOrigins__0`.

## Acceptance criteria
- Preflight from `http://localhost:3000` returns `Access-Control-Allow-Credentials: true`.
- Preflight from another origin is rejected.

@@@ P0-08
title: Create InitialCreate migration
type: task
area: database
phase: 0
priority: P0
size: S
depends: P0-02, P0-03, P0-04, P0-06
@@@
## Summary
No migrations exist. Create the first one covering Identity plus existing domain tables.

## Tasks
- [ ] Add `dotnet-ef` local tool manifest (`dotnet new tool-manifest`, `dotnet tool install dotnet-ef`).
- [ ] Run `dotnet ef migrations add InitialCreate -o Data/Migrations`.
- [ ] Review the generated SQL for FK behaviors (note the `Restrict` FKs; see P2-07).
- [ ] Apply with `dotnet ef database update` against local Postgres.
- [ ] Document migration rules: one migration per PR, coordinate with the team (per README).

## Implementation details
- Enable the `citext` extension later in P4-01; do not add it here.
- Never edit an applied migration; add a new one.

## Acceptance criteria
- Fresh database is created with all tables; migration committed with its snapshot.

@@@ P0-09
title: Create api.Tests project with real-Postgres integration harness
type: test
area: testing
phase: 0
priority: P0
size: M
depends: P0-08
@@@
## Summary
FK cascade and unique-index behavior cannot be verified with EF InMemory. Use a real Postgres.

## Tasks
- [ ] Create `api.Tests` (xUnit) referencing `Lorebound.Api`; add to a solution file.
- [ ] Add `Testcontainers.PostgreSql` and a shared `PostgresFixture` (one container per test run).
- [ ] Add `CustomWebApplicationFactory` overriding the connection string; apply migrations at startup.
- [ ] Provide helpers: `CreateUserAsync`, `LoginClientAsync` (cookie-carrying `HttpClient`), and a per-test data reset (Respawn or transaction-per-test).
- [ ] One smoke test: `GET /api/health` returns 200.

## Implementation details
- Docker must be available locally; document the requirement, and allow `TEST_DB_CONNECTION` env override to use an existing Postgres instead.
- Keep `HttpClient` with `HandleCookies = true` so cookie auth behaves like a browser.

## Acceptance criteria
- `dotnet test` passes locally and can run in CI.

@@@ P0-10
title: GitHub Actions CI for API and frontend
type: task
area: devex
phase: 0
priority: P1
size: S
depends: P0-09
@@@
## Summary
PRs into `dev` need automatic verification.

## Tasks
- [ ] `.github/workflows/ci.yml` triggered on pull requests to `dev` and `main`.
- [ ] API job: setup .NET 10, `dotnet restore`, `dotnet build --no-restore`, `dotnet test`.
- [ ] Frontend job: Node 22+, `npm ci`, `npm run lint`, `npm run build` in `frontend/`.
- [ ] Cache NuGet and npm.
- [ ] Name jobs `api` and `frontend` so branch protection can require them.

## Implementation details
- `ubuntu-latest` runners include Docker for Testcontainers.
- Use `paths` filters only if both jobs are still required checks (otherwise skipped jobs block merges).

## Acceptance criteria
- A test PR shows both checks and they pass.

@@@ P0-11
title: README example ports and API URL do not match launchSettings
type: bug
area: docs
phase: 0
priority: P3
size: XS
depends:
@@@
## Summary
README shows `https://localhost:7001` / `http://localhost:5001` and `NEXT_PUBLIC_API_URL=https://localhost:7001`, but `api/Properties/launchSettings.json` uses `https://localhost:7254` and `http://localhost:5110`.

## Steps to reproduce
1. Follow README "Running the API" and "Frontend Environment Variables".
2. Run `dotnet run` and compare the printed URLs.

## Expected
README examples match the actual ports (or state clearly they are examples).

## Tasks
- [ ] Update the README API examples to 7254/5110.
- [ ] Update the `.env.local` example.
- [ ] Remove `Services/` from the tree until it exists (or add it in the phase that needs it).

## Acceptance criteria
- Following the README verbatim reaches the health endpoint.

@@@ P0-12
title: Make PR closing keywords work for PRs merged into dev
type: task
area: devex
phase: 0
priority: P1
size: S
labels: needs-decision
depends:
@@@
## Summary
Team PRs merge into `dev`, but GitHub only auto-closes issues ("Closes #12") when the PR merges into the repository **default branch**. If the default is `main`, merged work leaves issues open and the project board never reaches Done.

## Decision needed (pick one)
1. **Set the default branch to `dev`** (simplest; `main` is updated only by release PRs). Recommended.
2. Keep `main` default and add a workflow that closes issues referenced by PRs merged into `dev`.

## Tasks
- [ ] Decide and record the choice in the README.
- [ ] Option 1: Settings, General, Default branch, switch to `dev`; update README clone/branch text.
- [ ] Option 2: workflow on `pull_request` `closed` (merged, base `dev`) that parses `Closes #N` and closes those issues using `GITHUB_TOKEN` (`issues: write`).
- [ ] Verify with a throwaway PR.

## Acceptance criteria
- Merging a PR containing `Closes #N` into `dev` closes issue N and moves the board item to Done.

@@@ P0-13
title: Commit project-board docs, issue forms and PR template to dev
type: chore
area: docs
phase: 0
priority: P2
size: XS
depends:
@@@
## Summary
`docs/project-board/`, `.github/ISSUE_TEMPLATE/*`, and `.github/pull_request_template.md` are generated locally and must reach `dev` through a PR.

## Tasks
- [ ] Review the files with the team.
- [ ] Open a PR from a feature branch into `dev`.
- [ ] Do not commit any token; the setup script reads `GITHUB_TOKEN` from the environment only.

## Acceptance criteria
- Files exist on `dev`; new issues use the forms.

@@@ P0-14
title: Protect main and dev branches
type: task
area: devex
phase: 0
priority: P1
size: XS
depends: P0-10
@@@
## Summary
Enforce the README rule "no direct commits to main" and require CI.

## Tasks
- [ ] Ruleset or branch protection for `main` and `dev`: require pull request, at least 1 approval, required status checks `api` and `frontend`, block force pushes and deletions.
- [ ] Enable "Automatically delete head branches".
- [ ] Require conversation resolution before merge.
- [ ] Allow admin bypass only for the repo owner (optional).

## Acceptance criteria
- Direct push to `dev` is rejected; a PR with failing CI cannot merge.

@@@ P0-15
title: Local onboarding script (scripts/onboard.ps1)
type: task
area: devex
phase: 0
priority: P2
size: S
depends: P0-09
@@@
## Summary
Onboarding is several manual steps (Docker, `.env`, compose, user-secrets, tools, migrations) and the easy ones to get wrong, like a password mismatch between `.env` and the user secret, fail with unclear errors. Add one idempotent PowerShell script that gets a developer from a fresh clone to a migrated local database.

## Tasks
- [ ] Add `scripts/onboard.ps1`, run from the repo root, with numbered steps and a clear success summary.
- [ ] Check prerequisites: .NET SDK 10.x and Docker Desktop running (the daemon, not just the CLI).
- [ ] If `.env` is missing, create it from `.env.example` with a random `POSTGRES_PASSWORD`. Never overwrite an existing `.env`.
- [ ] `docker compose up -d` and wait until `lorebound-postgres` is healthy.
- [ ] If `ConnectionStrings:DefaultConnection` is not set in user-secrets, set it from the `.env` values. Never overwrite an existing secret.
- [ ] `dotnet tool restore`, `dotnet restore`, then `dotnet ef database update` (skippable with `-SkipDbUpdate`).
- [ ] Friendly errors with a non-zero exit code: Docker not running, database not healthy, and "password authentication failed" pointing to README "Changing the database password".
- [ ] README: make the script the primary setup path ("run after cloning, and again after pulling a schema change"), keeping the manual steps as a fallback.

## Implementation details
- Safe to re-run at any time; it only creates what is missing.
- Local development only; CI (P0-10) and the test harness (P0-09) provision their own databases and do not use it.
- PowerShell only for now; add `onboard.sh` when a teammate needs it.
- If P0-09 adds new local prerequisites (e.g. Docker for Testcontainers), check them here too.

## Acceptance criteria
- On a fresh clone with Docker Desktop running, `./scripts/onboard.ps1` then `dotnet run` (in `api/`) gives `/api/health` `"database": "connected"`, with no other manual steps.
- Running it a second time changes nothing and succeeds.
- A mismatched password produces the README pointer and a non-zero exit code.

@@@ P0-16
title: Frontend lint errors fail the CI frontend check
type: bug
area: frontend
phase: 0
priority: P1
size: S
depends:
@@@
## Summary
`npm run lint` in `frontend/` fails with 6 errors (and 1 warning), all in pages merged in #158, #160 and #162. The new CI `frontend` check (P0-10, #52) runs lint, so it stays red on every PR until these are fixed. `npm run build` passes.

## Steps to reproduce
1. `cd frontend`
2. `npm ci`
3. `npm run lint`

## Expected
`npm run lint` exits 0 and the CI `frontend` check passes.

## Errors
| File | Line | Rule |
| --- | --- | --- |
| `app/builder/page.tsx` | 54 | `react/no-unescaped-entities` |
| `app/dashboard/page.tsx` | 21 | `react-hooks/set-state-in-effect` |
| `app/settings/page.tsx` | 25 | `react-hooks/set-state-in-effect` |
| `app/settings/[settingId]/page.tsx` | 70 | `react-hooks/set-state-in-effect` |
| `app/settings/[settingId]/entries/[entryId]/page.tsx` | 47 | `react-hooks/set-state-in-effect` |
| `components/settings/SettingEntryDialog.tsx` | 70 | `react-hooks/set-state-in-effect` |
| `app/settings/[settingId]/page.tsx` | 81 | warning: `@typescript-eslint/no-unused-vars` |

## Tasks
- [ ] Escape the quote/apostrophe in `builder/page.tsx` (e.g. `&apos;` or `{"'"}`).
- [ ] Replace the `setState`-in-`useEffect` patterns (see https://react.dev/learn/you-might-not-need-an-effect): read from the local stores during render or with a lazy `useState(() => ...)` initializer, and for `SettingEntryDialog` reset form state by giving the dialog a `key` tied to the entry instead of syncing in an effect.
- [ ] Remove the unused variable.
- [ ] Do not disable the rules to get a pass.

## Implementation details
- These pages will be rewired to the API in Phase 9; keep the fixes minimal.

## Acceptance criteria
- `npm run lint` exits 0 locally and the CI `frontend` check is green.

