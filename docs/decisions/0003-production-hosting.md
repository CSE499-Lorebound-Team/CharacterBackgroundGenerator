# ADR 0003: Host the course demo on Vercel, Render and Supabase

- **Status:** Accepted
- **Date:** 2026-10-12 (decided in the team channel on 2026-10-10)
- **Issue:** P8-08 to P8-11 (added by this decision)
- **Builds on:** [ADR 0001](0001-same-origin-api-proxy.md) (same-origin API proxy)
- **Supersedes:** the single-VM proposal in PR #216 (closed, not merged)

## Context

ADR 0001 fixed the production topology: the browser only calls `/api/*` on
the frontend's origin, Next.js rewrites those requests to `API_ORIGIN`, and
the `lorebound.auth` cookie stays host-only on the frontend's host. The
rewrite is in `frontend/next.config.ts`. It did not decide where the app runs.

PR #216 proposed one VM running Docker Compose, with TLS, deploy automation
and backups (tasks P8-08 to P8-14). The team rejected that scope. Production
is a **temporary course-demo environment**: it must be live and reachable, it
should be free, and it should cost as little operations work as possible, so
the remaining time goes to frontend and API integration, testing and polish.

## Decision

| Part | Host | Built from |
| --- | --- | --- |
| Next.js frontend | **Vercel** (free plan) | `frontend/` |
| ASP.NET Core API | **Render** web service (free plan, Docker) | `api/` |
| PostgreSQL | **Supabase** (free plan) | EF Core migrations |

- Vercel and Render both build from the same branch of this repo.
- ADR 0001 is kept unchanged. The browser calls `/api/*` on the Vercel
  origin, and Vercel forwards those requests to the Render API through
  `API_ORIGIN`. Auth, cookies and CSRF need no redesign.
- Supabase is used **only as a PostgreSQL host**. The API reaches it through
  `ConnectionStrings__DefaultConnection` like any other Postgres. Supabase
  Auth, its client libraries and the Supabase–Vercel integration are not used:
  the integration adds database variables to Vercel, but the frontend never
  connects to the database.
- Local development does not change: `docker-compose.yml` Postgres, the API on
  `localhost:5110` and the frontend on `localhost:3000`.

Out of scope for the demo: a custom domain, deploy automation beyond each
host's built-in "deploy on push", backups, monitoring, and more than one API
instance.

```text
Browser ──► https://<app>.vercel.app            (Next.js on Vercel)
              │  /api/*  ──rewrite (public HTTPS)──►  https://<api>.onrender.com/api/*
              ▼                                          (API container on Render)
                                                              │  TLS, session pooler
                                                              ▼
                                                        Supabase Postgres
```

## What changes compared with ADR 0001

**The API is public.** ADR 0001 allowed the API to sit on a private network.
On Render it has a public `onrender.com` URL, because Vercel reaches it over
the internet. This is acceptable: the auth cookie belongs to the Vercel host,
so a browser calling Render directly sends no cookie, and the CSRF check still
requires an allowlisted `Origin`.

**Client IPs for rate limiting are not reliable.** Requests reach the API
through Vercel and then Render's own proxy, neither with fixed IPs, so
`ForwardedHeaders:KnownProxies` cannot be pinned. Without trusted proxies the
API ignores `X-Forwarded-For`, and the auth rate limit (10 requests per 60
seconds) may count **all users as one client**. For the demo this is handled
by configuration, not code: P8-11 checks the partition key on the deployed
API, and if it is shared, raises `RateLimiting__Auth__PermitLimit` enough for
a demo audience. Trusting unverifiable forwarded headers is not done.

## Gaps the hosts expose

1. **Data Protection keys must be persisted (P8-08).** Render's free plan
   stops idle services and its filesystem is not kept. Each restart would
   generate new keys, sign everyone out and invalidate emailed links. Store
   the keys in Postgres with `PersistKeysToDbContext`.
2. **Registration fails without an email sender (P8-09).** Registration always
   sends a confirmation email (`AuthController.Register`), and outside
   Development `UnconfiguredEmailSender` throws. The user is created, but the
   request returns 500. For the demo, email is turned off (see "Email"
   below).
3. **Render has no native .NET runtime (P8-10).** The API needs a Dockerfile.
4. **Migrations are not applied by the app.** They are applied by hand
   (P8-11), never by `Migrate()` at startup.

## Configuration

### Vercel (project root directory `frontend/`)

| Variable | Value | Notes |
| --- | --- | --- |
| `API_ORIGIN` | `https://<api>.onrender.com` | Used by the rewrite at build time; changing it needs a redeploy |

### Render (Docker web service, root directory `api/`)

| Variable | Value | Why |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Production defaults (no OpenAPI, no seed data) |
| `PORT` | `8080` | Render routes to this port; the .NET container image listens on 8080 |
| `ConnectionStrings__DefaultConnection` | Supabase **session pooler** string, with `SSL Mode=Require;Gss Encryption Mode=Disable` | The pooler is reachable over IPv4; Supabase's direct host is IPv6-only on the free plan. The GSS setting stops Npgsql looking for Kerberos, which the API image does not include (P8-10) |
| `Cors__AllowedOrigins__0` | `https://<app>.vercel.app` | The CSRF check requires the browser's `Origin` to be allowlisted |
| `App__FrontendBaseUrl` | `https://<app>.vercel.app` | Required by config validation; also the base of invite links |
| `AllowedHosts` | `<api>.onrender.com` | P8-07; confirm the `Host` that Vercel's rewrite sends |
| `RateLimiting__Auth__PermitLimit` | Only if P8-11 finds a shared partition | See above |
| `Email__Provider` | `None` | Email off (P8-09) |
| `Auth__RequireConfirmedEmail` | `false` | Accounts can log in without confirming, since no email is sent |

Health check path on Render: `/api/health`.

Only the Vercel **production** URL is in `Cors:AllowedOrigins`. Vercel preview
deployments have other origins, so logging in from a preview fails the CSRF
check. That is expected.

### Supabase

- Create one project; copy the session pooler connection string to Render.
- **Turn off the Data API** (or remove `public` from its exposed schemas).
  EF Core creates every table, including `AspNetUsers`, in `public`, and the
  Data API would otherwise serve them to anyone holding the project's anon
  key. The app never uses the Data API.

## Demo-day risks

- **Cold starts.** A free Render service sleeps after inactivity, and the
  first request can take close to a minute. Open `/api/health` a few minutes
  before demoing.
- **Paused database.** Supabase pauses free projects after a period of
  inactivity. Check it is active the day before; resuming it is a button in
  the dashboard.
- **Free-plan limits** can change. Re-check the current limits of each host
  before the demo.

## Email

**Email is turned off for the demo** (team decision, 2026-10-12). A real
SMTP provider was rejected as extra setup for a demo project.

- P8-09 adds a sender that sends nothing, registered only when
  `Email:Provider` is `None`. Render sets that and
  `Auth__RequireConfirmedEmail=false`.
- Registration and login work. Resend-confirmation and forgot-password still
  return 204, but no email arrives, so a demo user who forgets a password
  registers a new account.
- The sender never logs links or codes. Logging them (as
  `ConsoleEmailSender` does in Development) is not allowed in production,
  because the links grant account access.
- Without `Email:Provider`, Production keeps `UnconfiguredEmailSender`, so a
  missing setting fails loudly instead of silently dropping email.

## Account owners

Decided by the team on 2026-10-12, instead of the shared login first
suggested here. Each host belongs to whoever works on that part of the app:

| Host | Owner | Why |
| --- | --- | --- |
| Render (API) | Tolaji (@Tolaji) | Works on `api/` |
| Supabase (database) | Tolaji (@Tolaji) | Goes with the API; only the API connects to it |
| Vercel (frontend) | Bugbear777 (@Bugbear777) | Works on `frontend/` |

- The owner creates the account, the project or service, and its settings,
  and is the only one who sees its secrets.
- The demo now depends on two people. If an owner is unavailable on demo
  day, nobody else can change that host. Where a free plan allows a second
  member, the owner invites a teammate as backup.
- How each owner sets up their host is in the runbook,
  [docs/deployment.md](../deployment.md) (P8-11).

## Consequences

- ADR 0001's settings and code stay as they are. The only differences are
  that the API is public and that client IPs cannot be trusted.
- New work is four small Phase 8 tasks (P8-08 to P8-11). PR #216's
  proposals for a deploy workflow, Caddy, a production Compose file and
  backups are dropped.
- P8-07 (security review) covers these hosts: `AllowedHosts` on Render, the
  Supabase Data API, and the CORS/CSRF re-check against the Vercel origin.
- If the app outlives the course, revisit this decision. A paid plan or the
  single-VM plan from PR #216 would remove the cold starts, pauses and
  rate-limit caveats.
