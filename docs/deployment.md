# Deploying the course demo

How to stand up, update and check the demo environment from
[ADR 0003](decisions/0003-production-hosting.md) (P8-11):

- the Next.js frontend on **Vercel**;
- the API on **Render**;
- PostgreSQL on **Supabase**.

The browser only talks to the Vercel URL. Vercel forwards `/api/*` to Render
([ADR 0001](decisions/0001-same-origin-api-proxy.md)), so the sign-in cookie
belongs to the Vercel host.

```text
Browser ──► https://<app>.vercel.app ──/api/*──► https://<api>.onrender.com ──► Supabase Postgres
```

**Secrets never go in this file, the repo, an issue or a PR.** They live
only in each host's settings. Where a value is secret, this file says so and
shows a placeholder.

## Contents

- [Owners](#owners)
- [Before you start](#before-you-start)
- [1. Supabase](#1-supabase-tolaji)
- [2. Apply the migrations](#2-apply-the-migrations-tolaji)
- [3. Render](#3-render-tolaji)
- [4. Vercel](#4-vercel-bugbear777)
- [5. Connect Render to the Vercel URL](#5-connect-render-to-the-vercel-url-tolaji)
- [6. Check the deployment](#6-check-the-deployment)
- [Every variable](#every-variable)
- [Day to day](#day-to-day)
- [Demo day](#demo-day)
- [Troubleshooting](#troubleshooting)

## Owners

Decided in ADR 0003, "Account owners":

| Host | Owner | Holds these secrets |
| --- | --- | --- |
| Supabase | Tolaji | Database password, connection string |
| Render | Tolaji | Connection string (as an environment variable) |
| Vercel | Bugbear777 | None: `API_ORIGIN` is a public URL |

Each owner creates the account and the project with their own login.
Where the free plan allows a second member, invite one teammate as backup.

## Before you start

- **GitHub access.** Render and Vercel deploy from
  `CSE499-Lorebound-Team/CharacterBackgroundGenerator`. Connecting it installs
  each host's GitHub app on the organization, which an **organization owner**
  may need to approve. Ask for that first.
- **Branch.** Both hosts deploy from **`dev`**, which has every merged PR and
  passes CI, and redeploy on every push to it.
  - To deploy only reviewed releases instead, point both hosts at `main` and
    merge `dev` into `main` to release.
  - Both hosts must use the same branch.
- **Names.** Pick them up front, because the hosts need each other's URLs:
  - the Render service, e.g. `lorebound-api`, gives
    `https://lorebound-api.onrender.com`;
  - the Vercel project, e.g. `lorebound`, gives `https://lorebound.vercel.app`.

  If a name is taken, the host adds a suffix. **Use the URLs the hosts
  actually show** everywhere below.
- **Region.** Put Supabase and Render in the same region (e.g. both in the US
  West or both in Frankfurt). Every API request makes database round trips,
  so distance between them slows every page.
- **Tools on the machine that applies migrations:** the repo cloned, the .NET
  10 SDK, and `dotnet tool restore` run once in the repository root (it
  installs `dotnet ef`).

## 1. Supabase (Tolaji)

1. **Create a project** on the free plan in the chosen region.
   - Generate a strong database password and keep it in your password
     manager. It is only typed into Render and into the migration command.
2. **Turn off the Data API.**
   - In the project settings, find the **Data API** and turn it off, or
     remove `public` from its exposed schemas.
   - Why: EF Core creates every table, including `AspNetUsers`, in `public`,
     and the Data API would serve them to anyone holding the project's public
     anon key. The app never uses the Data API.
   - The dashboard's advisor may warn that the tables have no row-level
     security. That is expected: only the API connects, as the database
     owner.
3. **Copy the session pooler connection string.** Open **Connect** and copy
   the **Session pooler** string (port 5432). The direct host is IPv6-only on
   the free plan, and Render needs IPv4.
4. **Convert it to the format the API reads**, adding the two settings at the
   end:

   ```text
   Host=<pooler host>;Port=5432;Database=postgres;Username=postgres.<project ref>;Password=<database password>;SSL Mode=Require;Gss Encryption Mode=Disable
   ```

   - `SSL Mode=Require` encrypts the connection.
   - `Gss Encryption Mode=Disable` stops Npgsql looking for Kerberos, which
     the API image lacks (P8-10).

   This string is a **secret**.

## 2. Apply the migrations (Tolaji)

The API never migrates at startup. Apply the migrations from your machine,
before the first deploy and again whenever a PR with a new migration is about
to merge:

```powershell
cd api
dotnet ef database update --connection "<connection string from step 1>"
```

- `--connection` keeps the string out of every file. It does stay in your
  PowerShell history, so clear that afterwards if others use the machine.
- Success ends with `Done.`. A connection or certificate error here would
  also stop the API, so fix it now (see [Troubleshooting](#troubleshooting)).
- **Never** run `dotnet run -- --seed` against Supabase. The seeder only
  works in Development (P8-02), and the demo creates its data through the
  app.

## 3. Render (Tolaji)

1. **New → Web Service**, connect the GitHub repository, and set:

   | Setting | Value |
   | --- | --- |
   | Name | the chosen service name |
   | Branch | `dev` (see [Before you start](#before-you-start)) |
   | Root directory | `api` |
   | Runtime / language | Docker |
   | Dockerfile path and build context | `api/Dockerfile` and `api/`. Render may show them relative to the root directory, as `./Dockerfile` and `.` |
   | Instance type | Free |
   | Region | same as Supabase |
   | Health check path | `/api/health` |
   | Auto-deploy | on commit |

2. **Environment variables.** Set the first block now, before the first
   deploy:

   | Variable | Value |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `PORT` | `8080` |
   | `ConnectionStrings__DefaultConnection` | **secret**: the string from step 1 |
   | `Email__Provider` | `None` |
   | `Auth__RequireConfirmedEmail` | `false` |
   | `App__FrontendBaseUrl` | `https://<app>.vercel.app` (the planned name for now) |
   | `Cors__AllowedOrigins__0` | `https://<app>.vercel.app` (the planned name for now) |

   Leave `AllowedHosts` and `RateLimiting__Auth__PermitLimit` unset until
   step 5 and step 6.
3. **Deploy.** The first deploy builds the image (a few minutes). When it is
   live, check:

   ```powershell
   curl.exe -s https://<api>.onrender.com/api/health
   ```

   It should answer `{"status":"healthy",...,"database":"connected"}`.
   `"unreachable"` (HTTP 503) means the connection string or Supabase is
   wrong: check the logs on Render.
4. Note the real Render URL and send it to the Vercel owner.

## 4. Vercel (Bugbear777)

1. **Add New → Project**, import the GitHub repository, and set:

   | Setting | Value |
   | --- | --- |
   | Framework preset | Next.js |
   | Root directory | `frontend` |
   | Production branch | `dev` (Settings → Git, after the import) |
   | Node.js version | 22 (as in CI) |

2. **Environment variable:** `API_ORIGIN` = `https://<api>.onrender.com`,
   without a trailing slash.
   - Set it for **Production** and **Preview**.
   - It is read **at build time**, by the rewrite in `frontend/next.config.ts`.
     If it is missing, the build points `/api/*` at `http://localhost:5110`
     and every API call fails.
   - Changing it needs a **redeploy**.
3. **Deploy.**
4. **Give production a stable, public domain:**
   1. Settings → Domains → Add.
   2. Add a free `.vercel.app` name, e.g. `lorebound-demo.vercel.app`
      (`lorebound.vercel.app` belongs to someone else's site).
   3. Connect it to the production branch.
   4. Send `https://<that name>` to the Render owner. It is the `<app>` URL
      everywhere in this runbook.

   **Do not use the URLs Vercel shows after a deploy**, such as
   `https://lorebound-<hash>-<team>-projects.vercel.app` or
   `https://lorebound-<team>-projects.vercel.app`:
   - The `<hash>` URL changes with every deploy, so the API would refuse
     sign-in after the next one.
   - Vercel Authentication (Settings → Deployment Protection) hides all of
     these behind a Vercel login, so visitors never reach the app.
   - The domain added here is the production domain. With the default
     ("Standard") protection it is public, while previews stay protected.
     Check by opening it in a private window: you should see the app, not a
     Vercel login.
5. **Preview deployments** (one per PR) get their own URLs. Calls to the API
   work there, but **signing in and every write fail with 403** ("Origin not
   allowed"), because only the production URL is allowed (ADR 0003). That is
   expected. Test sign-in on the production URL only.

## 5. Connect Render to the Vercel URL (Tolaji)

1. If the real Vercel URL differs from the planned one, update
   `App__FrontendBaseUrl` and `Cors__AllowedOrigins__0` on Render.
2. Add `AllowedHosts` = `<api>.onrender.com`. That is the host name only: no
   `https://`, no slash. Requests for any other host then get 400 (P8-07).
3. Save. Render redeploys. Then **check both** of these, because a wrong
   `AllowedHosts` takes the site down:
   - the deploy goes live, meaning Render's health check passed;
   - signing in through the Vercel URL works (step 6).

   If either fails with 400, see [Troubleshooting](#troubleshooting).

## 6. Check the deployment

Work through this list on the **Vercel URL** once everything is deployed, and
tick the boxes in #228. The PowerShell examples use:

```powershell
$app = "https://<app>.vercel.app"
$api = "https://<api>.onrender.com"
```

### The app works (P8-11 acceptance)

- [ ] In the browser on `$app`: register, log in, log out, log in again,
  create a setting.
  - Until Phase 9 wires the pages to the API, do this through the API
    instead (below).
- [ ] The sign-in cookie `lorebound.auth` belongs to the Vercel host. In the
  browser's developer tools, under Application → Cookies, check that:
  - it is listed under `$app`;
  - `Secure`, `HttpOnly` and `SameSite=Lax` are set;
  - it has no `Domain`.

Through the API, from PowerShell (the session keeps the cookie):

```powershell
$s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$h = @{ "X-Requested-With" = "Lorebound"; "Origin" = $app }
$account = '{"email":"demo1@example.com","password":"demo-passphrase-1","displayName":"Demo One"}'
Invoke-RestMethod "$app/api/auth/register" -Method Post -WebSession $s -Headers $h -ContentType "application/json" -Body $account
Invoke-RestMethod "$app/api/auth/login" -Method Post -WebSession $s -Headers $h -ContentType "application/json" -Body '{"email":"demo1@example.com","password":"demo-passphrase-1"}'
Invoke-RestMethod "$app/api/settings" -Method Post -WebSession $s -Headers $h -ContentType "application/json" -Body '{"name":"Demo world","description":"Created by the deploy check"}'
Invoke-RestMethod "$app/api/dashboard" -WebSession $s
Invoke-RestMethod "$app/api/auth/logout" -Method Post -WebSession $s -Headers $h
```

`Invoke-RestMethod` throws on an error status. If one throws, the message
says which step failed.

### Security checklist (P8-07, `docs/security/p8-07-security-review.md`)

- [ ] **Supabase Data API is off.** This must **not** return user rows. Get
  the anon key and project URL from Supabase → Connect.

  ```powershell
  curl.exe -s "https://<project ref>.supabase.co/rest/v1/AspNetUsers?select=Email" -H "apikey: <anon key>"
  ```

  An error or 404 is right. A JSON list of emails means the Data API is still
  exposing `public`: turn it off (step 1).
- [ ] **Secrets only in host settings.** The connection string appears
  nowhere in the repo, issues, PRs or chat.
  - `git grep -i "pooler.supabase"` finds nothing.
- [ ] **Production mode.** Neither `$api/openapi/v1.json` nor `$api/scalar`
  is served: anonymous callers get 401, like any unknown route.
- [ ] **One allowed origin.** `Cors__AllowedOrigins__0` is the Vercel
  production URL, and nothing else is set.
- [ ] **`AllowedHosts`** is set (step 5). The health check is green and
  sign-in through `$app` works.
- [ ] **HSTS and HTTPS.**
  - `curl.exe -sI "$app/api/health"` shows `strict-transport-security`.
  - `curl.exe -sI "http://<api>.onrender.com/api/health"` redirects to
    `https://`.
- [ ] **Cookie flags** are as above: `Secure`, `HttpOnly`, `SameSite=Lax`, no
  `Domain`.

### Rate-limit partition (ADR 0003)

Behind Vercel and Render the API may see every user as the same client, so
all users would share one login limit: 10 attempts per minute in total. To
check:

1. **You:** send 11 failed logins within a minute:

   ```powershell
   Set-Content login.json '{"email":"nobody@example.com","password":"not-the-password"}' -NoNewline -Encoding ascii
   1..11 | ForEach-Object { curl.exe -s -o NUL -w "%{http_code}`n" -X POST "$app/api/auth/login" -H "Content-Type: application/json" -H "X-Requested-With: Lorebound" -H "Origin: $app" --data-binary "@login.json" }
   Remove-Item login.json
   ```

   Expect `401` ten times, then `429`.
2. **Someone on a different network** (e.g. a phone hotspot), within that
   minute: one login attempt.
   - **401:** each client has its own limit. Nothing to change.
   - **429:** the limit is shared. On Render, set
     `RateLimiting__Auth__PermitLimit` to e.g. `100`, so a demo audience
     signing in at once is not refused, and record the finding in #228.
     - The invite preview and accept limit uses the same setting.
     - Never trust forwarded IP headers to fix this (ADR 0003).

## Every variable

No secret values here. Owners keep them in the host's settings.

### Render (API)

| Variable | Value | Secret | Why |
| --- | --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | no | JSON logs, HSTS, no `/scalar`, no seeding |
| `PORT` | `8080` | no | Tells Render the port; the image listens on 8080 |
| `ConnectionStrings__DefaultConnection` | Supabase session pooler string with `SSL Mode=Require;Gss Encryption Mode=Disable` | **yes** | The database |
| `Email__Provider` | `None` | no | Email off for the demo (P8-09) |
| `Auth__RequireConfirmedEmail` | `false` | no | Accounts sign in without confirming, since no email is sent |
| `App__FrontendBaseUrl` | `https://<app>.vercel.app` | no | Base of invite links |
| `Cors__AllowedOrigins__0` | `https://<app>.vercel.app` | no | The CSRF check accepts writes from this origin only |
| `AllowedHosts` | `<api>.onrender.com` | no | Other `Host` headers get 400 (P8-07) |
| `RateLimiting__Auth__PermitLimit` | only if the partition is shared | no | See "Rate-limit partition" |

### Vercel (frontend)

| Variable | Value | Secret | Why |
| --- | --- | --- | --- |
| `API_ORIGIN` | `https://<api>.onrender.com` | no | Where `/api/*` is forwarded; read at build time |

**Supabase:** no variables. Data API off. The database password exists only
inside the connection string.

## Day to day

- **Deploys:**
  - Merging into `dev` redeploys both hosts automatically.
  - The API's Data Protection keys are in the database (P8-08), so a
    redeploy or a restart does not sign anyone out.
- **A PR with a migration:**
  - Apply it to Supabase (step 2) right before merging. This project's
    migrations only add tables, columns and indexes, so the running API keeps
    working with them applied.
  - A migration that removes or renames something needs a plan in its PR
    first.
- **Changing a variable:**
  - On Render, saving redeploys the API.
  - On Vercel, redeploy for the change to apply.
- **Rotating the database password:**
  1. Change it in Supabase.
  2. Update the connection string on Render.
  3. Check `/api/health`.
- **Never delete rows from `DataProtectionKeys`:** everyone would be signed
  out and every invite or reset link would stop working.

## Demo day

- **Wake the API:** open `https://<api>.onrender.com/api/health` a few
  minutes before. A sleeping free service can take close to a minute to
  start.
- **The day before:** check that Supabase is active (free projects pause
  after inactivity; resuming is a button in the dashboard).
- **Free-plan limits:** re-check each host's current limits.
- **Bring the owners:** Tolaji for Render and Supabase, Bugbear777 for
  Vercel. They are the only people who can change those hosts.

## Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| Health check answers 503 with `"database": "unreachable"` | Wrong connection string, Supabase paused, or the direct (IPv6) host used | Use the session pooler string; resume the project |
| Migration or API fails with an SSL or certificate error | The TLS setting does not match what the pooler offers | Check the string against Supabase's connection settings; ask in the team channel before turning certificate checks off |
| Every API call from the site fails; the browser shows calls to `localhost:5110` | `API_ORIGIN` missing at build time | Set it on Vercel and redeploy |
| Sign-in or any write answers 403 "Origin not allowed" | Using a preview URL, or `Cors__AllowedOrigins__0` differs from the real Vercel URL | Use the production URL; fix the variable exactly (`https://`, no slash) |
| Every request answers 400 after setting `AllowedHosts` | Vercel or Render's health check sends a different `Host` | Remove `AllowedHosts` to restore the site, note the cause in #228, then try `<api>.onrender.com;<app>.vercel.app` |
| Logs show `libgssapi_krb5.so.2` | `Gss Encryption Mode=Disable` missing | Add it to the connection string (harmless otherwise) |
| Registration answers 500 | `Email__Provider` not `None` | Set it (P8-09) |
| Logs show "No XML encryptor configured" once | Expected: keys are stored unencrypted (P8-08) | None |
