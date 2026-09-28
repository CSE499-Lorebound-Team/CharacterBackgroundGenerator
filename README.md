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
├── api.Tests/             (xUnit tests for the API)
├── docs/project-board/    (roadmap and issue definitions)
├── Lorebound.slnx         (solution: api + api.Tests)
├── docker-compose.yml     (local PostgreSQL)
├── .env.example           (copy to .env)
├── dotnet-tools.json      (pinned dotnet-ef version)
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

Docker runs the local PostgreSQL database. Install [Docker Desktop](https://www.docker.com/products/docker-desktop/), start it, then verify:

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
git clone https://github.com/Bugbear777/CharacterBackgroundGenerator.git
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

## Development Branch

`dev` is the shared integration branch used by the development team.

Completed feature branches should be merged into `dev` through Pull Requests.

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

The terminal will display the addresses used by the API.

For example:

```text
Now listening on: https://localhost:7001
Now listening on: http://localhost:5001
```

The exact port numbers may differ depending on your machine.

## Test the API

If the API contains the health endpoint, navigate to:

```text
https://localhost:<PORT>/api/health
```

For example:

```text
https://localhost:7001/api/health
```

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
http(s)://localhost:<API PORT>
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

If automated tests are added later, they should also be run before submitting a Pull Request.

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

After your feature branch is merged into `dev`, update your local copy:

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
NEXT_PUBLIC_API_URL=https://localhost:7001
```

The exact API URL may differ on each developer's machine.

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

The API uses PostgreSQL 17, run locally with Docker Compose. Do this once after cloning.

## 1. Create your `.env` file

From the repository root, copy the example and change `POSTGRES_PASSWORD`:

```powershell
Copy-Item .env.example .env
```

`.env` is git-ignored. Never commit it.

## 2. Start the database

From the repository root:

```powershell
docker compose up -d
```

This starts a `lorebound-postgres` container on port 5432 with data kept in the `lorebound-pgdata` volume. Check it with `docker compose ps`; stop it with `docker compose down` (add `-v` to also delete the data).

## 3. Give the API the connection string

The connection string is stored with .NET user secrets, outside the repository. From the `api/` folder, using the values from your `.env`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=lorebound;Username=lorebound;Password=<POSTGRES_PASSWORD from .env>"
```

Never put credentials in `appsettings*.json`. Outside development, set the `ConnectionStrings__DefaultConnection` environment variable instead.

## 4. Create the tables

From the `api/` folder:

```powershell
dotnet tool restore
dotnet ef database update
```

This applies every migration in `api/Data/Migrations/`. Run it again whenever you pull changes that add a migration.

## 5. Verify

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
3. the configured API URL matches the port shown by `dotnet run`
4. CORS is configured to allow the frontend
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