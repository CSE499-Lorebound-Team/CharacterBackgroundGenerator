# CharacterBackgroundGenerator

Joseph's Quote "Not all who wander are lost" - J.R.R. Tolkien
'Tola Osibo's quote "Obedience is better than sacrifice" - 1 Samuel 15:22

# Lorebound

Lorebound is a narrative-focused character background builder for tabletop role-playing games.

Unlike traditional character builders that primarily focus on game mechanics such as classes, ability scores, equipment, and combat statistics, Lorebound helps players build a character's history, relationships, motivations, and connections to a campaign setting.

The application allows Game Masters to define information about their setting and allows players to use that information through a guided character background creation process.

---

# Table of Contents

- [Repository Structure](#repository-structure)
- [Prerequisites](#prerequisites)
- [Cloning the Repository](#cloning-the-repository)
- [Branching and Git Workflow](#branching-and-git-workflow)
- [Where to Run Git Commands](#where-to-run-git-commands)
- [Running the Frontend](#running-the-frontend)
- [Running the API](#running-the-api)
- [Running the Full Application](#running-the-full-application)
- [Testing](#testing)
- [Testing Frontend and API Together](#testing-frontend-and-api-together)
- [Submitting Changes](#submitting-changes)
- [Creating a Pull Request](#creating-a-pull-request)
- [Keeping Your Branch Updated](#keeping-your-branch-updated)
- [After a Pull Request Is Merged](#after-a-pull-request-is-merged)
- [Environment Configuration](#environment-configuration)
- [Database Development](#database-development)
  - [Quick Setup (Recommended)](#quick-setup-recommended)
  - [Changing the Database Password](#changing-the-database-password)
- [Pulling New Dependencies](#pulling-new-dependencies)
- [Common Development Workflow](#common-development-workflow)
- [Troubleshooting](#troubleshooting)
- [Team Development Rules](#team-development-rules)

---

# Repository Structure

Lorebound uses a **monorepo**.

This means the frontend and backend API are stored inside the same Git repository, even though they are separate applications.

```text
CharacterBackgroundGenerator/
│
├── frontend/
│   ├── app/
│   ├── components/
│   ├── public/
│   ├── package.json
│   └── ...
│
├── api/
│   ├── Controllers/
│   ├── Data/
│   │   └── Migrations/
│   ├── Dtos/
│   ├── Errors/
│   ├── Mapping/
│   ├── Models/
│   ├── Program.cs
│   ├── Lorebound.Api.csproj
│   ├── README.md          (API conventions)
│   └── ...
│
├── api.Tests/             (xUnit tests; real Postgres via Testcontainers)
├── docs/project-board/    (roadmap and issue definitions)
├── docs/decisions/        (architecture decision records)
├── Lorebound.slnx         (solution: api + api.Tests)
├── docker-compose.yml     (local PostgreSQL)
├── .env.example           (copy to .env)
├── dotnet-tools.json      (pinned dotnet-ef version)
├── scripts/onboard.ps1    (one-command local setup)
├── README.md
└── .gitignore
```

### Frontend

The `frontend` directory contains the Next.js application.

Technology:

- Next.js
- React
- TypeScript
- Tailwind CSS
- shadcn/ui

### API

The `api` directory contains the ASP.NET Core backend.

Technology:

- ASP.NET Core
- C#
- Entity Framework Core
- PostgreSQL

The frontend and API are stored in the same Git repository but run as separate applications.

---

# Prerequisites

Before working on the project, install the following tools.

## Git

Verify that Git is installed:

```powershell
git --version
```

## Node.js

Node.js and npm are required to run the frontend.

Verify installation:

```powershell
node --version
npm --version
```

## .NET SDK

The .NET SDK is required to build and run the API.

Verify installation:

```powershell
dotnet --version
```

## Docker Desktop

Docker runs the local PostgreSQL database and the throwaway database used by the automated API tests. Install [Docker Desktop](https://www.docker.com/products/docker-desktop/), start it, then verify:

```powershell
docker compose version
```

## Entity Framework Core Tools

Required for database migrations. The version is pinned in `dotnet-tools.json` at the repository root, so everyone uses the same one. From the repository root:

```powershell
dotnet tool restore
dotnet ef --version
```

## Recommended Development Tools

The following are recommended but not required:

- Visual Studio 2022 or later
- Visual Studio Code
- GitHub Desktop
- Postman or another API testing tool

---

# Cloning the Repository

Clone the repository from GitHub:

```powershell
git clone https://github.com/CSE499-Lorebound-Team/CharacterBackgroundGenerator.git
```

Enter the project folder:

```powershell
cd CharacterBackgroundGenerator
```

The team development branch is:

```text
dev
```

Switch to the development branch:

```powershell
git checkout dev
```

Pull the latest changes:

```powershell
git pull origin dev
```

Next, set up the local database: with Docker Desktop running, run `powershell -ExecutionPolicy Bypass -File .\scripts\onboard.ps1` from the repository root (see [Database Development](#database-development)).

You are now ready to begin development.

---

# Branching and Git Workflow

Do not develop directly on `main`.

Do not normally develop directly on `dev`.

The branch structure is:

```text
main
  |
  └── dev
       |
       ├── feature/frontend-shell
       ├── feature/api-setup
       ├── feature/setting-management
       ├── feature/database-models
       └── feature/character-builder
```

## Main Branch

`main` represents the stable version of the project.

Changes should only reach `main` after they have been tested and accepted.

## Branch Protection

`main` and `dev` are protected by the repository ruleset **Protect main and dev** (defined in `.github/rulesets/protect-main-and-dev.json`). It applies to everyone, including admins:

- Direct pushes are rejected; every change arrives through a Pull Request.
- The CI checks `api` and `frontend` must pass before merging.
- All review conversations must be resolved before merging.
- Force pushes and deleting the branch are blocked.

Approvals are not required, so the author may merge their own Pull Request once CI is green. Merged feature branches are **not** deleted automatically; keep using your feature branch.

## Development Branch

`dev` is the shared integration branch used by the development team.

Completed feature branches should be merged into `dev` through Pull Requests.

The repository's default branch stays `main`, so GitHub opens new Pull Requests against `main`: **always change the base branch to `dev`**. Also, GitHub only auto-closes issues (`Closes #N`) for merges into the default branch, so merging into `dev` does **not** close issues. Closing is a manual step (see [After a Pull Request Is Merged](#after-a-pull-request-is-merged)).

## Feature Branches

Before beginning work, always update your local copy of `dev`:

```powershell
git checkout dev
git pull origin dev
```

Then create a new branch:

```powershell
git checkout -b feature/your-feature-name
```

For example:

```powershell
git checkout -b feature/setting-editor
```

or:

```powershell
git checkout -b feature/character-builder
```

Try to keep each branch focused on one feature or task.

---

# Where to Run Git Commands

Git commands can generally be run from the repository root:

```text
CharacterBackgroundGenerator/
```

For example:

```powershell
git status
git add .
git commit
git push
```

The frontend and API are **not separate Git repositories**.

Do not run:

```powershell
git init
```

inside `frontend` or `api`.

There should only be one `.git` directory, located at the repository root.

---

# Running the Frontend

The frontend is located in:

```text
frontend/
```

From the repository root:

```powershell
cd frontend
```

## Install Dependencies

The first time you clone the project, run:

```powershell
npm install
```

You should also run this again whenever new npm packages are added to the project.

## Start the Frontend

Run:

```powershell
npm run dev
```

The Next.js development server should start.

The frontend is normally available at:

```text
http://localhost:3000
```

Open that address in your browser.

## Stop the Frontend

Press:

```text
Ctrl + C
```

in the terminal running the frontend.

---

# Running the API

The API is located in:

```text
api/
```

Open a second terminal.

From the repository root:

```powershell
cd api
```

## Restore Dependencies

The first time you clone the project, run:

```powershell
dotnet restore
```

This downloads the required NuGet packages.

The API also needs a database connection string. Complete [Database Development](#database-development) once before the first run; without it the API stops at startup with a message explaining what to set.

## Start the API

Run:

```powershell
dotnet run
```

The terminal will display the address used by the API:

```text
Now listening on: http://localhost:5110
```

The ports are fixed in `api/Properties/launchSettings.json`, so they are the same on every machine. To also listen on HTTPS (`https://localhost:7254`), run:

```powershell
dotnet run --launch-profile https
```

## Test the API

Open the health endpoint in a browser:

```text
http://localhost:5110/api/health
```

The requests in `api/Lorebound.Api.http` use the same address and can be sent from VS Code (REST Client extension) or Visual Studio.

A successful response should look similar to:

```json
{
  "status": "healthy",
  "application": "Lorebound API",
  "database": "connected"
}
```

If the database is not running, the endpoint returns `503` with `"database": "unreachable"`.

## Stop the API

Press:

```text
Ctrl + C
```

in the terminal running the API.

---

# Running the Full Application

The frontend and API must run at the same time during normal development.

Use two terminals.

## Terminal 1 - Frontend

From the repository root:

```powershell
cd frontend
npm run dev
```

## Terminal 2 - API

From the repository root:

```powershell
cd api
dotnet run
```

The development environment should now look approximately like this:

```text
Browser
   |
   v
Next.js Frontend
http://localhost:3000
   |
   | HTTP / JSON
   v
ASP.NET Core API
http://localhost:5110
   |
   v
Database
```

The frontend communicates with the API through HTTP requests.

Both applications must be running for features that require backend data.

---

# Testing

Before submitting a Pull Request, test the part of the project you changed.

At minimum, make sure:

- the frontend starts successfully
- the API starts successfully
- the application compiles
- the page or feature you changed works as expected
- existing features still work
- the browser console contains no unexpected errors
- the API terminal contains no unexpected exceptions

## Continuous Integration (CI)

Every Pull Request into `dev` or `main` runs `.github/workflows/ci.yml` on GitHub Actions. It has two checks, both shown at the bottom of the PR:

| Check | Runs |
| --- | --- |
| `api` | `dotnet restore`, `dotnet build`, `dotnet test` (with a real PostgreSQL container), and a check that every model change has a migration |
| `frontend` | `npm ci`, `npm run lint`, `npm run build` in `frontend/` |

Both must pass before merging; branch protection enforces this on `dev` and `main` (see [Branch Protection](#branch-protection)). If one fails, open **Details** on the check to see the log, fix the problem locally with the same command, and push again; CI re-runs automatically. You can also re-run it from the repository's **Actions** tab.

---

## Frontend Testing

From:

```text
frontend/
```

run:

```powershell
npm run dev
```

Manually test the affected pages.

Also run:

```powershell
npm run lint
```

If a production build needs to be verified, run:

```powershell
npm run build
```

The build should complete without errors before merging major frontend changes.

---

## API Testing

From:

```text
api/
```

run:

```powershell
dotnet build
```

The project should build successfully with no errors.

Then run:

```powershell
dotnet run
```

Test the affected endpoint using:

- a browser
- Swagger
- Postman
- another API client
- the frontend application

## Automated API Tests

The API has an xUnit test project in `api.Tests/`. Run all tests before submitting a Pull Request. From the repository root:

```powershell
dotnet test Lorebound.slnx
```

**Docker Desktop must be running.** Database tests start their own throwaway PostgreSQL 17 container through Testcontainers. It is separate from your `docker compose` database, which the tests never touch. The first run pulls the image and takes a minute or so; later runs take seconds.

To use an existing PostgreSQL instead of a container, set `TEST_DB_CONNECTION`. Tests **delete all data** in that database, so its name must contain `test`, otherwise the run is refused:

```powershell
$env:TEST_DB_CONNECTION = "Host=localhost;Port=5432;Database=lorebound_test;Username=lorebound;Password=<password>"
dotnet test Lorebound.slnx
Remove-Item Env:TEST_DB_CONNECTION
```

### Writing tests

| Test needs | Use |
| --- | --- |
| No database (JSON, errors, CORS, pure logic) | `IClassFixture<ApiFactory>` |
| The real database | Inherit `PostgresTestBase` and add `[Collection(PostgresCollection.Name)]` |

Database tests share one migrated database, and every test starts with empty tables. Helpers on `CustomWebApplicationFactory`:

- `CreateUserAsync(email, displayName, password)` creates a confirmed user through `UserManager` (password defaults to `CustomWebApplicationFactory.DefaultPassword`).
- `CreateSignedInClientAsync(email, displayName)` creates a confirmed user, signs it in through `POST /api/auth/login`, and returns `(Client, User)`; the client keeps the auth cookie.
- `WithConfig(key, value)` (on any factory) returns a copy with one config value overridden, e.g. `Factory.WithConfig("Auth:RequireConfirmedEmail", "true").CreateCookieClient()`.
- `CreateCookieClient()` returns an `HttpClient` that keeps cookies like a browser, for cookie auth. It uses `https://localhost` because the auth cookie is `Secure`.
- Every client from either factory sends `X-Requested-With: Lorebound`, like the real frontend, so requests pass the CSRF check. To test the check itself, remove it: `client.DefaultRequestHeaders.Remove(CsrfProtectionMiddleware.HeaderName)`.
- The test-only controllers under `/test` are available in both factories; `GET /test/auth/protected` returns 204 only for a signed-in client. Every endpoint requires sign-in by default, so a new test-only controller that should be public needs `[AllowAnonymous]`.
- `WithDbAsync(db => ...)` runs a query with a fresh `LoreboundDbContext`.
- `Emails` records what the API "sent" (`Factory.Emails.Sent`: kind, user id, address, link or code) in place of a real sender; it is cleared before each test.

### Auth test suite

The authentication surface (P1-13) is covered end to end, mostly against the real database (`Security/CsrfTests` and `Security/AuthenticationTests` need none). Where to find each check:

| Requirement | Tests |
| --- | --- |
| Register, confirm, log in, use the session, log out, end to end | `Auth/AuthJourneyTests` |
| Login cookie is `HttpOnly`, `Secure`, `SameSite=Lax`; session vs 14-day cookie; only the auth cookie is set | `Auth/LoginTests`, `Auth/AuthJourneyTests`, `Security/AuthenticationTests` |
| No response carries a `token`, `access` or `refresh` field, or the cookie value; no token-issuing routes are mapped | `Auth/AuthJourneyTests`, `Security/AuthenticationTests` |
| Logout ends the session everywhere, including a copied cookie; a tampered cookie is rejected | `Auth/LogoutTests`, `Auth/AuthJourneyTests` |
| Lockout after 5 failures (423), per account only; same 401 for unknown email, wrong password and unconfirmed email; unknown emails never report a lockout | `Auth/LoginTests`, `Auth/AuthJourneyTests` |
| Register validation and duplicate handling | `Auth/RegisterTests` |
| Confirm email and resend: happy and failure paths | `Auth/ConfirmEmailTests` |
| Forgot and reset password: happy and failure paths; old sessions end | `Auth/ResetPasswordTests` |
| Rate limit: the 11th request in a minute returns 429 | `Security/RateLimitTests` |
| CSRF header and Origin checks | `Security/CsrfTests`, `Auth/AuthJourneyTests` |
| Anonymous calls to protected routes return 401 problem JSON; only intended endpoints are public | `Security/FallbackPolicyTests`, `Auth/UsersMeTests` |
| `ICurrentUser` and `/api/users/me` | `Auth/CurrentUserTests`, `Auth/UsersMeTests` |
| Account deletion: password re-entry (400); 409 listing owned settings that have other members, deleting nothing; otherwise everything the user owns goes and nobody else's data is touched; signed out and every session ends | `Auth/DeleteAccountTests` |

Run just these with `dotnet test Lorebound.slnx --filter "FullyQualifiedName~Auth|FullyQualifiedName~Security"`.

### Sharing test suite

Invites are a security boundary, so sharing (P3-08) is covered through HTTP against the real database. `TestSupport/SharingWorld` seeds one setting with an owner, a GameMaster, a Player, a signed-in non-member and an anonymous client. Where to find each check:

| Requirement | Tests |
| --- | --- |
| Expired, revoked, exhausted, unknown and malformed codes all return the identical 404, on preview and accept | `Sharing/InvitePreviewTests`, `Sharing/InviteAcceptTests` |
| Parallel accepts of a 1-use code create exactly one membership; parallel accepts by one user create one membership and use one use | `Sharing/InviteAcceptTests` |
| Accepting when already a member returns 200 and uses nothing | `Sharing/InviteAcceptTests` |
| The owner cannot be demoted, removed or leave | `Sharing/SettingMembersTests`, `Sharing/MemberRemovalTests` |
| A Player cannot create, list or revoke invites, change roles or remove others | `Sharing/InviteCreateTests`, `Sharing/InviteManagementTests`, `Sharing/SettingMembersTests`, `Sharing/MemberRemovalTests` |
| A non-member gets 404 on every sharing endpoint, identical to a missing setting; anonymous gets 401 | `Sharing/SharingBoundaryTests` (plus each endpoint's own matrix) |
| No sharing response contains an email; the members list never does | `Sharing/SharingBoundaryTests`, `Sharing/SettingMembersTests` |
| An invite joins only its own setting; another setting's invites and members cannot be reached through this setting's URLs; a deleted setting's codes stop working | `Sharing/SharingBoundaryTests`, `Sharing/InviteManagementTests` |
| Code lookups are rate limited (10/min, preview and accept share one counter, separate from login) | `Security/RateLimitTests` |
| Invite storage: unique codes, `UseCount` within `MaxUses`, cascade with the setting; code format | `Data/SettingInviteTests`, `Sharing/InviteCodesTests`, `Sharing/InviteCodeNormalizeTests` |

Run just these with `dotnet test Lorebound.slnx --filter "FullyQualifiedName~Sharing|FullyQualifiedName~SettingInvite"`.

### Entries test suite

GM-only lore must never reach a Player, so entries (P4-07) are covered through HTTP against the real database, reusing `TestSupport/SharingWorld` (its `AddEntryAsync` and `LinkAsync` insert entries and relationships directly). Where to find each check:

| Requirement | Tests |
| --- | --- |
| No Player response (entries list, type filter, search, `gmOnly=true`, entry detail, setting detail, settings list) contains a GM-only entry's id, name, description or the `isGmOnly` flag | `Entries/EntriesBoundaryTests` |
| A Player asking for a GM-only entry gets the identical 404 as for a missing one | `Entries/EntriesBoundaryTests`, `Entries/EntriesDetailTests` |
| `relationshipCount`, settings list `entryCount` and detail `entryCountsByType` exclude hidden entries and links to them for Players only | `Entries/EntriesBoundaryTests`, `Entries/EntriesListTests` |
| Relationships to GM-only entries are left out of a Player's entry detail | `Entries/EntriesDetailTests` |
| Anonymous 401 and non-member 404 (identical to a missing setting) on every entries endpoint | `Entries/EntriesBoundaryTests` |
| Player writes are 403, whether the entry is public, hidden or missing | `Entries/EntriesBoundaryTests` (plus each endpoint's own tests) |
| Delete is 409 with `relationshipCount` and `characterCount` unless `force=true`; force removes only that entry's relationships and keeps characters with a null choice (P6-09) | `Entries/EntriesDeleteTests` |
| Names are unique per (setting, type) ignoring case, in the database and as 409 from create and update, including concurrent creates | `Data/SettingEntryTests`, `Entries/EntriesCreateTests`, `Entries/EntriesUpdateTests` |
| Validation, search wildcards, paging and type filter | `Entries/EntriesCreateTests`, `Entries/EntriesUpdateTests`, `Entries/EntriesListTests` |
| Relationship storage: self-links and duplicate (source, target, type) links rejected by the database; type and description length limits | `Data/SettingEntryRelationshipTests` |

Run just these with `dotnet test Lorebound.slnx --filter "FullyQualifiedName~Entries|FullyQualifiedName~SettingEntry"`.

### Relationships test suite

A link must never reveal a GM-only entry to a Player, so relationships (P5-06) are covered through HTTP against the real database, reusing `TestSupport/SharingWorld` like the entries suite. `Relationships/RelationshipsBoundaryTests` checks every relationship endpoint at once; the other files test one endpoint each. Where to find each check:

| Requirement | Tests |
| --- | --- |
| No Player response (relationships list, `entryId` and `type` filters, entries list, entry detail) contains a GM-only entry's id, name or description, or the id or type of a link to it | `Relationships/RelationshipsBoundaryTests`, `Entries/EntriesDetailTests` |
| A Player never sees a link where either end is GM-only, in the list or its `totalCount`; hiding an entry hides its links at once, unhiding restores them | `Relationships/RelationshipsListTests`, `Relationships/RelationshipsBoundaryTests` |
| `entryId` returns incoming and outgoing links; an entry the caller cannot see (hidden, missing or of another setting) is the same 404 | `Relationships/RelationshipsListTests`, `Relationships/RelationshipsBoundaryTests` |
| Cross-setting links are 400 and create nothing; another setting's links are the same 404 as a missing one on `PUT` and `DELETE` and never listed; the setting comes from the route | `Relationships/RelationshipsCreateTests`, `Relationships/RelationshipsBoundaryTests` |
| Self-link 400; duplicate 409, including concurrent creates; another type or direction is allowed | `Relationships/RelationshipsCreateTests`, `Data/SettingEntryRelationshipTests` |
| Update changes only type and description; changing source or target is 400; delete is 204 and keeps the entries | `Relationships/RelationshipsUpdateDeleteTests` |
| Types: normalized (trim, single spaces, suggested spelling), compared ignoring case in the database and as 409; `GET /api/relationship-types` | `Relationships/RelationshipTypesTests`, `Data/SettingEntryRelationshipTests` |
| Anonymous 401 and non-member 404 (identical to a missing setting) on every relationship endpoint | `Relationships/RelationshipsBoundaryTests` (plus each endpoint's own tests) |
| Player writes are 403 with the identical problem, whether the link is public, to a hidden entry or missing | `Relationships/RelationshipsBoundaryTests` |
| Deleting an entry is 409 while it has links; with `force=true` its links in both directions go and the rest stay | `Relationships/RelationshipsBoundaryTests`, `Entries/EntriesDeleteTests` |

Run just these with `dotnet test Lorebound.slnx --filter "FullyQualifiedName~Relationship"`.

### Characters test suite

Characters are owned by one player, readable by their setting's GameMasters, and read-only once the owner is removed, so Phase 6 (P6-12) is covered through HTTP against the real database, reusing `TestSupport/SharingWorld` (its `AddCharacterAsync`, `AddChoiceAsync` and `RemoveMemberAsync` set up characters and removals). `Characters/CharactersBoundaryTests` checks every character endpoint against every kind of caller at once; the other files test one endpoint or rule each. Where to find each check:

| Requirement | Tests |
| --- | --- |
| Ownership matrix: owner (member), owner (removed), GameMaster, setting owner, other player, non-member and anonymous against detail, update, delete, the GameMaster list and create; refused writes change nothing | `Characters/CharactersBoundaryTests` |
| A character the caller may not see is the identical 404 to a missing one; `isOwner`/`isReadOnly` on the detail and the own list match what the caller may do | `Characters/CharactersBoundaryTests` |
| Lifecycle through the API: create, edit, removal makes it read-only, re-joining restores write, delete; deleting the setting deletes its characters (removed players' too); force-deleting a chosen entry leaves the character with a null choice | `Characters/CharacterLifecycleTests` |
| Character storage: draft defaults, status stored as its name, name and backstory limits; choices are an entry or free text but never both, unique per (character, step, ordinal), with text limits | `Data/CharacterTests` |
| Deleting a setting deletes its characters and their choices; deleting a character deletes its choices; deleting an entry keeps the choice with a null entry; a user who owns characters cannot be deleted | `Data/CharacterTests` |
| Access matrix: the owner who is still a member reads and writes; a removed owner reads and deletes only (403 on write, with a clear reason); a GameMaster of the setting reads only (403 on write and delete); everyone else and a missing character get the same 404; re-joining restores write access | `Auth/CharacterAccessTests` |
| Every controller under `api/characters` authorizes through `ICharacterAccess` | `Conventions/CharacterAccessConventionTests` |
| List: only the caller's own characters, newest update first; `status`, `settingId` and `search` filters (wildcards literal), paging; `homelandName` from the homeland choice; `isReadOnly` after removal | `Characters/CharactersListTests` |
| Create: any member (GameMasters too) starts a draft they own with defaults; name trimmed, blank becomes "Unnamed Character", over 100 is 400; non-member, removed player and missing setting get the same 404; anonymous 401 | `Characters/CharactersCreateTests` |
| Detail: choices resolved and sorted; owner reads and writes, a removed owner and the setting's GameMasters read only; another player, a non-member and a missing id get the same 404; a chosen entry that became GM-only keeps its name, a deleted one shows a null name | `Characters/CharactersDetailTests` |
| Update: the owner changes name (trimmed, 1-100) and free-text backstory (up to 10000, stored as sent, blank clears); `updatedAt` always moves; invalid values are 400 and change nothing; a removed owner and the setting's GameMasters get 403; others 404 | `Characters/CharactersUpdateTests` |
| Delete: the owner deletes (also when read-only after removal) and the choices go with it, the entries stay; a GameMaster gets 403 for another player's character but deletes their own; others 404 | `Characters/CharactersDeleteTests` |
| GameMaster view: every character of the setting (removed players' too, flagged `ownerIsMember: false`) with owner name, newest first; `status` and character-or-owner `search` filters, paging; Player 403, non-member 404 identical to a missing setting | `Characters/SettingCharactersListTests` |
| Read-only after removal (P6-10): removal by a GameMaster or the owner, or leaving, leaves characters and choices untouched; every character write route (found from the app's endpoints) is 403 with the read-only reason while GET, list and DELETE still work; a new invite restores write access; GameMasters still see the character | `Characters/CharacterReadOnlyTests` |
| Deleting an entry characters chose is 409 with `characterCount` (characters, not choices); `force=true` deletes it and leaves the characters intact with a null choice that still reads | `Entries/EntriesDeleteTests` |

Run just these with `dotnet test Lorebound.slnx --filter "FullyQualifiedName~Character"`.

### Builder test suite

Phase 7 checks the guided builder: the step catalog (P7-01), narrowed options (P7-02), saving choices (P7-03), stale detection (P7-04) and completion (P7-05).

| Requirement | Tests |
| --- | --- |
| Catalog: 8 steps, unique keys, contiguous orders, the agreed key/entry type/required per step; every `CharacterStepKeys` constant is a catalog key; steps that store nothing allow 0 selections; `GET /api/builder/steps` returns it in order (401 anonymous) | `Builder/BuilderStepsTests` |
| Options: a homeland linked to two cultures offers only those, links followed in either direction; no linked candidate (or no earlier choice) offers all, not narrowed; only the step's type in the character's setting; only earlier steps narrow; a Player never gets GM-only entries and hidden links or hidden chosen entries do not narrow; steps without an entry type have none; unknown step 404; owner-with-write only (removed owner and GameMaster 403, non-member and missing 404, anonymous 401) | `Builder/StepOptionsTests` |
| Save a choice: stores the entry or trimmed free text and returns the character; saving again replaces only that step; no answer clears it; `currentStep` only moves forward and `updatedAt` always moves; wrong type, too many selections, text on an entry step, entries on a text step and text over 2000 are 400 keyed by field; another setting's, hidden and missing entries get the identical 400; an entry that became GM-only after being chosen can be kept; GameMasters may choose GM-only entries for their own characters; `setting`/`review` 400, unknown step 404; only the owner with write access (removed owner, GameMaster and setting owner 403, non-member 404, anonymous 401), and refusals change nothing | `Builder/SaveChoiceTests`, `Characters/CharacterReadOnlyTests` |
| Stale steps: switching homeland flags the culture in the save response and on `GET`, keeps the choice, and a fitting answer clears it; several stale steps listed in step order; no narrowing means nothing stale; a new character and a deleted chosen entry are not stale; judged by the owner's view, so a link to a GM-only entry never flags a Player's step (for the GameMaster reader too); `staleSteps` on every character detail | `Builder/StaleStepsTests`, `Characters/CharactersCreateTests` |
| Complete and reopen: every required step answered completes; missing required steps, a stale step, a deleted or mistyped chosen entry and a blank name are 400 keyed by step (or `Name`) and change nothing; completing twice is a no-op; answers are 409 while complete and editable again after reopening; only the owner with write access (removed owner, GameMaster and setting owner 403, non-member 404, anonymous 401) | `Builder/CompleteReopenTests`, `Characters/CharacterReadOnlyTests` |

Run just these with `dotnet test Lorebound.slnx --filter "FullyQualifiedName~Builder"`.

---

# Testing Frontend and API Together

For features involving both applications:

1. Start the API.
2. Note the API URL shown in the terminal.
3. Start the frontend.
4. Open the frontend at:

```text
http://localhost:3000
```

5. Navigate to the feature being tested.
6. Perform the expected workflow.
7. Verify that the frontend receives the expected data from the API.
8. Watch both the browser console and API terminal for errors.

If the frontend cannot reach the API, verify:

- the API is running
- the correct API port is being used
- the frontend API URL is configured correctly
- CORS allows requests from the frontend
- HTTPS certificates are trusted if HTTPS is being used

---

# Submitting Changes

After completing and testing your work, check which files changed:

```powershell
git status
```

Stage your changes:

```powershell
git add .
```

Commit them:

```powershell
git commit -m "Describe the change"
```

Use short but meaningful commit messages.

Examples:

```text
Add setting creation form
Create character background API
Add database models
Fix navigation sidebar
Add character summary page
```

Push your feature branch:

```powershell
git push -u origin feature/your-feature-name
```

The `-u` option is normally only required the first time the branch is pushed.

Afterward, normal pushes can use:

```powershell
git push
```

---

# Creating a Pull Request

On GitHub, create a Pull Request from your feature branch into:

```text
dev
```

The direction should be:

```text
feature/your-feature-name
          |
          v
         dev
```

Do not create a Pull Request into `main` unless the team specifically intends to create a stable/release version.

In the description, add `Closes #N` for each board issue the PR completes. This links the PR to the issue on the board; it does not close the issue, because the PR merges into `dev`, not the default branch. Both CI checks (`api` and `frontend`) must pass.

Before requesting a merge, confirm:

- the project builds
- your feature works
- no unrelated files were modified
- no secrets or passwords were committed
- your branch includes the latest relevant changes from `dev`

---

# Keeping Your Branch Updated

Other developers may merge changes into `dev` while you are working.

Before beginning work each day, it is a good idea to update your local `dev`:

```powershell
git checkout dev
git pull origin dev
```

If you already have a feature branch and need the newest `dev` changes:

```powershell
git checkout dev
git pull origin dev
git checkout feature/your-feature-name
git merge dev
```

Resolve any merge conflicts before continuing.

After resolving conflicts:

```powershell
git add .
git commit
git push
```

---

# After a Pull Request Is Merged

After your feature branch is merged into `dev`, **close each issue the PR completed by hand**. Merges into `dev` do not close issues automatically, because the default branch is `main`. Add a comment saying what was done and naming the PR, for example:

```powershell
gh issue close 48 -R CSE499-Lorebound-Team/CharacterBackgroundGenerator -c "Done in <commit>, merged to dev via #<PR>. <What changed and how it was verified.>"
```

Or use **Close issue** on the issue page on GitHub. The project board then moves the card to **Done**.

Then update your local copy:

```powershell
git checkout dev
git pull origin dev
```

You may then delete your local feature branch:

```powershell
git branch -d feature/your-feature-name
```

If the remote branch is no longer needed:

```powershell
git push origin --delete feature/your-feature-name
```

Deleting completed branches is optional but helps keep the repository organized.

---

# Environment Configuration

Environment-specific values should not be hardcoded into the application.

Examples include:

- API URLs
- database connection strings
- passwords
- authentication secrets
- API keys

These values should be stored using environment variables, configuration files excluded from Git, or .NET user secrets.

Never commit passwords, API keys, or private credentials to GitHub.

---

## Frontend Environment Variables

Local frontend configuration may use:

```text
frontend/.env.local
```

For example:

```env
API_ORIGIN=http://localhost:5110
```

The browser never calls the API directly. The frontend forwards `/api/*` to `API_ORIGIN` (a server-only variable; the default matches the API port in `api/Properties/launchSettings.json`), so the auth cookie belongs to the frontend's own origin. `NEXT_PUBLIC_API_URL` is not used. See [ADR 0001](docs/decisions/0001-same-origin-api-proxy.md); the rewrite itself lands with P9-03. The API's CSRF check only accepts unsafe requests whose `Origin` is `http://localhost:3000` (set in `api/appsettings.Development.json`), so run the frontend on its default port.

Files containing local secrets should not be committed.

---

## API Configuration

ASP.NET Core configuration may use:

```text
appsettings.json
```

for non-sensitive configuration.

Sensitive development values should use environment variables or .NET user secrets.

For example:

```powershell
dotnet user-secrets init
```

Do not place database passwords or private credentials into files committed to Git.

---

# Database Development

The API uses PostgreSQL 17, run locally with Docker Compose.

## Quick setup (recommended)

With Docker Desktop running, from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\onboard.ps1
```

The script checks the .NET 10 SDK and Docker, creates `.env` with a random password if it is missing, starts the database, restores tools and packages, sets the connection-string user secret if it is missing, and applies migrations. It never overwrites an existing `.env` or user secret, so it is safe to re-run.

**Run it after cloning, and again whenever you pull changes that add a migration.** Use `-SkipDbUpdate` to skip the migration step. If it stops with an error, it prints what to do next; the manual steps below do the same thing one at a time.

## Manual setup

Do this once after cloning if you are not using the script.

### 1. Create your `.env` file

From the repository root, copy the example and change `POSTGRES_PASSWORD`:

```powershell
Copy-Item .env.example .env
```

`.env` is git-ignored. Never commit it.

### 2. Start the database

From the repository root:

```powershell
docker compose up -d
```

This starts a `lorebound-postgres` container on port 5432 with data kept in the `lorebound-pgdata` volume. Check it with `docker compose ps`; stop it with `docker compose down` (add `-v` to also delete the data).

### 3. Give the API the connection string

The connection string is stored with .NET user secrets, outside the repository. From the `api/` folder, using the values from your `.env`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=lorebound;Username=lorebound;Password=<POSTGRES_PASSWORD from .env>"
```

Never put credentials in `appsettings*.json`. Outside development, set the `ConnectionStrings__DefaultConnection` environment variable instead. Likewise set `App__FrontendBaseUrl` (the frontend address used in emailed links and invite join links). Behind a reverse proxy, also set `ForwardedHeaders__KnownProxies__0` to the proxy's IP so rate limiting sees real client IPs.

### 4. Create the tables

From the `api/` folder:

```powershell
dotnet tool restore
dotnet ef database update
```

This applies every migration in `api/Data/Migrations/`. Run it again whenever you pull changes that add a migration.

### 5. Verify

Run the API (`dotnet run` in `api/`) and open `/api/health`. It should report `"database": "connected"`.

## Changing the database password

Postgres reads `POSTGRES_PASSWORD` only the first time it creates the `lorebound-pgdata` volume. Editing `.env` afterwards does **not** change the password of the existing database. To change it:

1. Update `POSTGRES_PASSWORD` in `.env`.
2. Recreate the database. **This deletes all local data:**

   ```powershell
   docker compose down -v
   docker compose up -d
   ```

3. From `api/`, run the `dotnet user-secrets set` command from step 3 again with the new password.
4. Re-create the tables as in step 4, then verify `/api/health` as in step 5.

## Migrations

Migrations live in `api/Data/Migrations/`, together with `LoreboundDbContextModelSnapshot.cs`. The first one is `InitialCreate`. Run these commands from `api/`:

| Task | Command |
| --- | --- |
| Apply all migrations to your database | `dotnet ef database update` |
| Add a migration after changing models or `LoreboundDbContext` | `dotnet ef migrations add <DescriptiveName> -o Data/Migrations` |
| Undo your last migration, **only if it has not been pushed** | `dotnet ef migrations remove` |
| Check that no model change is missing a migration | `dotnet ef migrations has-pending-model-changes` |
| List migrations and which are applied | `dotnet ef migrations list` |

Rules:

- **One migration per PR**, and tell the team before merging a schema change, since it affects everyone's database.
- **Never edit or delete a migration once it is merged into `dev`.** Add a new migration to change it.
- Always commit the migration together with the updated model snapshot.
- If two PRs both add migrations, whoever merges second must remove theirs, pull `dev`, and re-add it so the snapshot stays consistent.

---

# Pulling New Dependencies

If another developer adds a frontend package, after pulling the latest changes run:

```powershell
cd frontend
npm install
```

If another developer adds or changes .NET packages, run:

```powershell
cd api
dotnet restore
```

If the pull adds a database migration (new files in `api/Data/Migrations/`), re-run the onboarding script, or `dotnet ef database update` from `api/`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\onboard.ps1
```

---

# Common Development Workflow

A normal development session should look like this:

```powershell
git checkout dev
git pull origin dev
git checkout -b feature/my-feature
```

Develop and test the feature.

Then:

```powershell
git status
git add .
git commit -m "Add my feature"
git push -u origin feature/my-feature
```

Create a Pull Request:

```text
feature/my-feature -> dev
```

After the PR is merged:

```powershell
git checkout dev
git pull origin dev
git branch -d feature/my-feature
```

---

# Troubleshooting

## `npm` is not recognized

Node.js is not installed or is not available in your system PATH.

Verify with:

```powershell
node --version
npm --version
```

Install Node.js if necessary, then restart your terminal.

---

## `dotnet` is not recognized

The .NET SDK is not installed or is not available in your system PATH.

Verify with:

```powershell
dotnet --version
```

Install the required .NET SDK and restart your terminal.

---

## Frontend packages are missing

From:

```text
frontend/
```

run:

```powershell
npm install
```

---

## API packages are missing

From:

```text
api/
```

run:

```powershell
dotnet restore
```

---

## The frontend does not start

Make sure you are inside:

```text
frontend/
```

Then run:

```powershell
npm install
npm run dev
```

Check the terminal output for errors.

---

## The API does not start

Make sure you are inside:

```text
api/
```

Then run:

```powershell
dotnet restore
dotnet build
dotnet run
```

Check the terminal output for errors.

If it stops with `Connection string 'DefaultConnection' is not configured`, complete [Database Development](#database-development).

---

## `password authentication failed for user "lorebound"`

`/api/health` returns `503` and the API log shows this error when the password in your user secret does not match the database.

1. Compare the `Password=` in `dotnet user-secrets list` (run in `api/`) with `POSTGRES_PASSWORD` in `.env`.
2. If they match but it still fails, `.env` was probably changed after the database was first created. Follow [Changing the database password](#changing-the-database-password).

## `/api/health` reports `"database": "unreachable"`

The database container is not running. From the repository root run `docker compose up -d`, and check that Docker Desktop is started and `docker compose ps` shows `lorebound-postgres` as healthy.

---

## HTTPS Certificate Warning

ASP.NET Core may use a local development HTTPS certificate.

If your machine does not trust the development certificate, run:

```powershell
dotnet dev-certs https --trust
```

Then restart the API.

---

## The Frontend Cannot Reach the API

Verify that:

1. the API is running
2. the frontend is running
3. `NEXT_PUBLIC_API_URL` in `frontend/.env.local` is `http://localhost:5110`, the address shown by `dotnet run`
4. the frontend runs on `http://localhost:3000`, the only origin CORS allows in development (`Cors:AllowedOrigins` in `api/appsettings.Development.json`)
5. your local environment configuration is correct

---

## `git push` Is Rejected

Your branch may be behind the remote branch.

First check:

```powershell
git status
```

If you are working on `dev`:

```powershell
git pull origin dev
```

If you are on a feature branch, update `dev` first and merge it into your feature branch.

---

## Merge Conflicts

Git will identify files containing conflicts.

Open the affected files and look for markers similar to:

```text
<<<<<<< HEAD
your changes
=======
incoming changes
>>>>>>> dev
```

Decide which code should remain, remove the conflict markers, then run:

```powershell
git add .
git commit
```

Afterward:

```powershell
git push
```

If you are unsure how a conflict should be resolved, coordinate with the developer responsible for the conflicting code before discarding either version.

---

# Team Development Rules

To reduce conflicts and keep the project stable:

1. Do not commit directly to `main`.
2. Prefer feature branches instead of working directly on `dev`.
3. Pull the latest `dev` before starting new work.
4. Keep branches focused on one feature or task.
5. Test your work before creating a Pull Request.
6. Do not commit secrets, passwords, API keys, or local environment files.
7. Do not run `git init` inside `frontend` or `api`.
8. Do not commit `node_modules`, build output, or generated temporary files.
9. Use descriptive commit messages.
10. Merge completed features into `dev` through Pull Requests.