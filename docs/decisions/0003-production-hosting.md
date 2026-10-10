# ADR 0003: Host production as one Docker Compose stack on a single VM

- **Status:** Proposed (draft for team review)
- **Date:** 2026-10-09
- **Issue:** None yet; see "Follow-up tasks" below
- **Builds on:** [ADR 0001](0001-same-origin-api-proxy.md) (same-origin API proxy)

## Context

ADR 0001 fixed the production *topology*: the browser talks only to the
Next.js origin, Next.js rewrites `/api/*` to an API that browsers cannot reach
directly, and the auth cookie stays host-only. It did not decide *where* that
runs, and nothing in the repo deploys anything yet.

What exists today:

- **Frontend:** Next.js 16 (`frontend/`). `next.config.ts` is still empty; the
  rewrite lands with P9-03.
- **API:** ASP.NET Core on .NET 10 (`api/`), EF Core with Npgsql.
- **Database:** PostgreSQL 17, run locally by `docker-compose.yml`.
- **CI:** `.github/workflows/ci.yml` builds and tests on PRs into `dev` and
  `main`. It does not build images or deploy.
- **No Dockerfiles**, no deploy workflow, no hosting issue on the board.

The current code has gaps that any production host has to close:

1. **Data Protection keys are not persisted.** The `lorebound.auth` cookie and
   the Identity email-confirmation and password-reset tokens are protected
   with Data Protection keys. With the default key store, a fresh container
   gets new keys, which signs everyone out and invalidates links already
   emailed.
2. **Migrations are not applied anywhere.** `Program.cs` never calls
   `Migrate()`, and that is deliberate. Production needs an explicit step.
3. **Email fails outside Development.** `EmailSetup` registers
   `UnconfiguredEmailSender`, which throws. `Auth:RequireConfirmedEmail` is
   `true` in `appsettings.json`, so production registration and password
   reset need a real provider.
4. **Forwarded headers only trust one hop.** `RateLimitingSetup` sets
   `KnownProxies` but leaves `ForwardLimit` at its default of 1. A TLS proxy
   in front of Next.js adds a second hop, so the API would see the TLS
   proxy's IP as the client and rate limiting (P1-08) would treat every user
   as one client.
5. **HSTS and `AllowedHosts`** are still open in P8-07.

Constraints: this is a course project with a small team, little traffic,
little budget, and a need for a stable demo URL. The API must stay private,
per ADR 0001.

## Options

1. **One VM running Docker Compose.** A small Linux VM runs Caddy (TLS),
   Next.js, the API, and Postgres as containers on one private network. Only
   Caddy publishes ports.
2. **Managed containers and a managed database** (for example Azure Container
   Apps with internal ingress for the API, plus Azure Database for
   PostgreSQL). No servers to patch, but more resources to configure and a
   higher fixed monthly cost. Proxy IPs are not fixed, so trusting forwarded
   headers needs a subnet rather than one address.
3. **Split platforms** (Vercel for Next.js, a PaaS for the API, a hosted
   Postgres). Vercel's rewrites reach the API over the public internet, so
   the API cannot be private, and Vercel's outbound IPs are not fixed, so
   `ForwardedHeaders:KnownProxies` cannot be pinned. This conflicts with
   ADR 0001.

## Decision (proposed)

**Option 1: one VM running a Docker Compose stack.**

- It extends the existing `docker-compose.yml` rather than adding a new
  platform, so production matches local dev closely.
- A private Compose network makes the API and database unreachable from
  outside without any cloud networking setup, as ADR 0001 requires.
- The proxy IPs are fixed and known, so forwarded headers can be trusted
  narrowly.
- One predictable bill and one place to look when something breaks.

The trade-off is that the team owns OS updates, backups, and recovery from
the VM failing. That is acceptable at this scale, and the backup step below
covers the data.

Option 2 is the upgrade path if the app ever needs no-downtime deploys or more
than one API instance. Nothing below blocks that move.

## How it works

```text
Internet ──443──► caddy ──► web (Next.js :3000) ──/api/* rewrite──► api (:8080) ──► db (Postgres :5432)
                  TLS, HSTS                                          │
                                                   migrate (one-shot, runs before api starts)
```

Services in `deploy/docker-compose.prod.yml`, all on one user-defined network
with a fixed subnet (for example `172.30.0.0/24`):

| Service | Image | Published ports | Notes |
| --- | --- | --- | --- |
| `caddy` | `caddy:2` | 80, 443 | Automatic Let's Encrypt TLS; reverse-proxies everything to `web:3000` |
| `web` | `ghcr.io/<org>/lorebound-web` | none | Next.js `output: "standalone"`; `API_ORIGIN=http://api:8080` at build time |
| `api` | `ghcr.io/<org>/lorebound-api` | none | `ASPNETCORE_ENVIRONMENT=Production`; starts after `migrate` succeeds |
| `migrate` | `ghcr.io/<org>/lorebound-migrate` | none | EF Core migration bundle (`dotnet ef migrations bundle`); exits when done |
| `db` | `postgres:17` | none | Named volume; the same image as local dev |

### Migrations

CI builds an EF Core migration bundle into the `migrate` image. Compose runs
it before the API (`depends_on: condition: service_completed_successfully`).
A failed migration stops the deploy, and the old API keeps running. This
keeps `Migrate()` out of app startup.

### Data Protection keys

Persist keys to Postgres with `PersistKeysToDbContext`
(`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`) and a
`DataProtectionKeys` table added by migration. The keys are then backed up
with everything else and survive redeploys. Set an explicit application name
so the keys stay valid if the image or path changes.

### Forwarded headers

The API sees two hops: Next.js connects to it, and Caddy is listed in
`X-Forwarded-For`. Add a `ForwardedHeaders:KnownNetworks` setting (the
Compose subnet) and set `ForwardLimit` to 2, so the API resolves the real
client IP. Verify with a request through the full stack that the rate-limit
partition key holds the client's IP and not Caddy's. ADR 0001 assumes Next.js
appends `X-Forwarded-For`, but no one has checked this on Next.js 16 yet.

### Email

Implement `IEmailSender<ApplicationUser>` for a transactional email provider,
as the comment in `Email/EmailSetup.cs` describes. Bind an `Email` config
section and pass the API key as an environment variable. The provider is an
open question (below).

### Secrets and configuration

Secrets live in a `.env` file on the VM, readable only by the deploy user and
never committed. The values come from ADR 0001 and the steps above:

| Variable | Value |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | `Host=db;Database=lorebound;Username=lorebound;Password=...` |
| `POSTGRES_PASSWORD` | Long random value |
| `Cors__AllowedOrigins__0` | `https://<domain>` |
| `App__FrontendBaseUrl` | `https://<domain>` |
| `ForwardedHeaders__KnownNetworks__0` | `172.30.0.0/24` |
| `AllowedHosts` | `<domain>` |
| `Email__*` | Provider settings and API key |
| `API_ORIGIN` (web build arg) | `http://api:8080` |

### Deploys

A new `deploy` workflow runs on pushes to `main`. `dev` stays the
integration branch, so merging `dev` into `main` is the release step.

1. Build the `web`, `api` and `migrate` images and push them to GitHub
   Container Registry, tagged with the commit SHA.
2. Connect to the VM over SSH (key in a GitHub Actions secret) and run
   `docker compose pull && docker compose up -d`.
3. Call `GET /api/health` through Caddy and fail the workflow if it does not
   return 200.

Rollback: redeploy the previous SHA tag. Migrations must stay
backward-compatible for one release (add before drop) so the previous image
still works against the migrated schema.

### Backups

A nightly `pg_dump` (cron on the VM, or a small Compose service) to off-VM
object storage, keeping 14 days. Test a restore once before the demo.

## Open questions for the team

1. **VM provider and who pays.** Any small Linux VM with Docker works (about
   1 vCPU and 2 GB RAM is enough). Student offers such as Azure for Students
   or the GitHub Student Developer Pack may cover the cost; confirm the
   current terms before choosing.
2. **Domain.** A custom domain, or a free subdomain from the provider? Caddy
   needs a DNS name pointing at the VM to issue certificates.
3. **Email provider.** Which transactional service, and from which sender
   address? Until one is configured, production can neither confirm accounts
   nor reset passwords. Turning off `Auth:RequireConfirmedEmail` for a demo
   would fix registration only, and is not recommended.
4. **Is production only a demo?** If the app is shut down after the course,
   backups and the deploy workflow could be simplified. This draft assumes
   real users' data needs to survive.

## Consequences

- New production config adds no new auth or CORS rules. The ADR 0001 values
  are reused as they are.
- Code changes are small and contained: Data Protection key persistence (with
  a migration), `KnownNetworks` and `ForwardLimit` in `RateLimitingSetup`, a
  real email sender, `output: "standalone"` in `next.config.ts`, and two
  Dockerfiles.
- A single VM means a short outage on each deploy and if the VM fails. That
  is acceptable for this app; Option 2 is the upgrade path.
- The team owns OS patching on the VM. Enable unattended security updates.

## Follow-up tasks (to add to the board if accepted)

| Proposed ID | Task | Depends on |
| --- | --- | --- |
| P8-08 | Persist Data Protection keys in Postgres | none |
| P8-09 | Trust the Compose subnet in forwarded headers (`KnownNetworks`, `ForwardLimit = 2`) | none |
| P8-10 | Production email sender | Open question 3 |
| P8-11 | Dockerfiles for `web`, `api` and `migrate`; `output: "standalone"` | P9-03 (the rewrite) |
| P8-12 | `deploy/docker-compose.prod.yml`, Caddyfile, `.env` template and runbook | P8-11 |
| P8-13 | `deploy` workflow: build, push to GHCR, SSH deploy, health check | P8-12, open questions 1 and 2 |
| P8-14 | Nightly `pg_dump` backup and a tested restore | P8-12 |

P8-07 (security review) already covers HSTS, production cookie flags and
`AllowedHosts`. Caddy can send the HSTS header if the API does not.
