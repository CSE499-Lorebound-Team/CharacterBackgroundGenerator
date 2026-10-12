@@@ E0
title: Phase 0 - Foundations (epic)
type: epic
area: api
phase: 0
priority: P0
size: L
depends:
@@@
## Goal
Give the API a production-shaped base before any feature work: Identity-ready user model, timestamps, conventions, real database config, first migration, tests, CI, and repo hygiene.

## Exit criteria
- `dotnet build` and `dotnet test` pass locally and in CI.
- `InitialCreate` migration applies to a fresh local Postgres.
- Scaffold code removed; conventions (DTOs, paging, errors, JSON) documented.
- PRs into `dev` can auto-close their linked issues; `main` and `dev` are protected.

## Decisions baked in
- All enums are stored as strings in Postgres.
- `User` becomes an ASP.NET Core Identity user **before** the first migration exists.

@@@ E1
title: Phase 1 - Authentication (epic)
type: epic
area: auth
phase: 1
priority: P0
size: L
depends:
@@@
## Goal
Real authentication using **httpOnly cookies only**. No access, refresh, or bearer tokens are ever returned in a response body or readable by JavaScript.

## Exit criteria
- Register, login, logout, email confirmation, password reset, and `/users/me` work end to end.
- Every endpoint except health and auth requires an authenticated user (fallback policy).
- Cookie flags asserted by tests: HttpOnly, Secure, SameSite=Lax.
- CSRF defense and auth rate limiting are active.
- Production topology decision recorded (frontend and API must be same-site).

## Decisions baked in
- Custom `AuthController` on `SignInManager` cookie sign-in. **`MapIdentityApi` is not used** because its login/refresh endpoints issue bearer tokens.
- Account deletion is delivered in Phase 6 (it needs settings, memberships, and characters).

@@@ E2
title: Phase 2 - Settings CRUD (epic)
type: epic
area: api
phase: 2
priority: P0
size: L
depends:
@@@
## Goal
Campaign settings with role-aware access: membership model, access helper, and full CRUD.

## Exit criteria
- Creating a setting also creates the creator's GameMaster membership.
- List, detail, update, delete behave per the permission matrix (non-member 404, player 403 on writes, only the **owner** can delete).
- Deleting a setting with entries and relationships works (cascade verified on Postgres).

## Decisions baked in
- Two roles only: `GameMaster` and `Player`. The creator is the owner (`OwnerUserId`); there are no co-owners.
- Non-members always receive 404 so a setting's existence is never leaked.

@@@ E3
title: Phase 3 - Sharing (epic)
type: epic
area: sharing
phase: 3
priority: P1
size: L
depends:
@@@
## Goal
GM-to-player sharing through invite codes and member management.

## Exit criteria
- GM can create, list, and revoke invite codes; a signed-in user can preview and accept one.
- Accept is idempotent, race-safe, and respects expiry, revocation, and max uses.
- Members can be listed, roles changed (owner only), removed, or leave; the owner can never be removed.

## Decisions baked in
- Invites grant the `Player` role only. Promotion to GM is an owner action on an existing member.

@@@ E4
title: Phase 4 - Setting Entries (epic)
type: epic
area: api
phase: 4
priority: P1
size: L
depends:
@@@
## Goal
Lore entries (locations, cultures, religions, and so on) with GM-only secrecy.

## Exit criteria
- Entries CRUD with type filter, search, and paging.
- Players never receive `IsGmOnly` entries from any endpoint (list, detail, search, counts, relationships).
- Delete is safe: 409 unless `?force=true`.

## Decisions baked in
- `IsGmOnly` flag on `SettingEntry` (secret lore is standard in TTRPGs).

@@@ E5
title: Phase 5 - Entry Relationships (epic)
type: epic
area: api
phase: 5
priority: P1
size: L
depends:
@@@
## Goal
Typed, directed links between entries that later narrow the builder's options.

## Exit criteria
- Relationships CRUD, GM-write and player-read, with hidden-entry filtering.
- DB enforces no self-links and no duplicate (source, target, type).
- Relationship-type vocabulary decided and documented (ADR).

@@@ E6
title: Phase 6 - Characters (epic)
type: epic
area: characters
phase: 6
priority: P1
size: L
depends:
@@@
## Goal
Character and choice models plus CRUD, GM read-only view, and account deletion.

## Exit criteria
- `Character` and `CharacterChoice` tables exist via migration.
- Players CRUD their own characters; GMs read characters in their setting.
- A player removed from a setting keeps their characters, **read-only**.
- `DELETE /users/me` is blocked while the user owns a setting that has other members.

## Decisions baked in
- Backstory is a free-text field only. No generated or AI-written backstory.

@@@ E7
title: Phase 7 - Builder Support (epic)
type: epic
area: builder
phase: 7
priority: P1
size: L
depends:
@@@
## Goal
Server-side support for the guided 8-step builder: step catalog, relationship-narrowed options, validated choice saving, completion.

## Exit criteria
- `GET /builder/steps`, options, choice save, complete, and reopen all work and are validated server-side.
- Changing an earlier step reports stale later choices instead of silently deleting them.

@@@ E8
title: Phase 8 - Dashboard and Polish (epic)
type: epic
area: api
phase: 8
priority: P2
size: L
depends:
@@@
## Goal
Dashboard aggregate, seed data, OpenAPI docs, observability, performance and security review, and the course-demo deployment (ADR 0003).

## Exit criteria
- `/api/dashboard` serves the dashboard page.
- Fresh clone can run with seed data in minutes.
- Security and performance review findings resolved or ticketed.
- The demo runs on Vercel, Render and Supabase, with a runbook in `docs/deployment.md`.

@@@ E9
title: Phase 9 - Frontend Integration (epic)
type: epic
area: frontend
phase: 9
priority: P1
size: L
depends:
@@@
## Goal
Replace every mock array in the Next.js app with real API calls and add auth pages and route protection.

## Exit criteria
- No hardcoded mock data or fake user remain.
- Login, register, reset, and invite-join flows work.
- Loading, error, and empty states exist on every data page.
- `npm run lint` and `npm run build` pass; e2e smoke test covers login to finished character.

## Notes
Read the Next.js docs in `frontend/node_modules/next/dist/docs/` before coding (see `frontend/AGENTS.md`); Next 16 conventions may differ from older versions.
