# ADR 0001: Serve the API through the frontend's origin (same-origin proxy)

- **Status:** Accepted
- **Date:** 2026-10-08
- **Issue:** P1-12 (#68)
- **Unblocks:** P9-03 (frontend API client)

## Context

The API authenticates with one httpOnly cookie, `lorebound.auth`
(`Secure`, `SameSite=Lax`, host-only: no `Domain` attribute). The browser
sends it only on requests to the host that set it, and `SameSite=Lax` keeps it
off cross-site requests.

Locally this works with the frontend on `localhost:3000` and the API on
`localhost:5110`, because ports do not change the "site". In production the
browser has to see the frontend and the API as the same site, or the cookie is
never sent.

## Options

1. **Same-origin proxy.** Next.js `rewrites()` forwards `/api/*` from the
   frontend's origin to the API. The browser only ever talks to one origin.
2. **Sibling subdomains.** `app.example.com` and `api.example.com`, with the
   cookie on `Domain=example.com`. The browser calls the API directly with CORS.
3. **Separate domains.** Needs `SameSite=None`. Rejected: it weakens CSRF
   protection, and browsers increasingly block third-party cookies.

## Decision

**Option 1, the same-origin proxy.**

- The cookie stays host-only on the frontend's host; the API's cookie settings
  do not change, and the cookie is never shared with other subdomains.
- The browser makes no cross-origin calls, so production needs no CORS
  configuration beyond the origin allowlist the CSRF check uses.
- It needs no custom domain for the API. The API can sit on any host or on a
  private network that only the Next.js server can reach.

Option 2 would also work, but needs two custom domains, a cookie `Domain`
setting, CORS in production, and sends the cookie to every subdomain.

## How it works

```text
Browser ──► https://app.example.com           (Next.js)
              │  /api/*  ──rewrite──►  API_ORIGIN/api/*
              ▼
            API (internal host, e.g. http://api:8080)
```

1. Browser code calls relative paths (`fetch("/api/users/me")`), with
   `credentials: "include"` and `X-Requested-With: Lorebound`.
2. Next.js forwards the request, cookie included, to the API. It adds
   `X-Forwarded-For` (the client IP) and `X-Forwarded-Proto`.
3. The API's `Set-Cookie` comes back through the frontend's origin, so the
   cookie belongs to `app.example.com`.

Frontend rewrite (added in P9-03):

```ts
// frontend/next.config.ts
const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${process.env.API_ORIGIN ?? "http://localhost:5110"}/api/:path*`,
      },
    ];
  },
};
```

Server components cannot use a relative URL. P9-03's server-side fetch calls
`API_ORIGIN` directly and forwards the incoming `cookie` header itself.

## Configuration

### Frontend

| Variable | Production | Local dev |
| --- | --- | --- |
| `API_ORIGIN` (server-only, no `NEXT_PUBLIC_`) | Internal API address, e.g. `http://api:8080` | `http://localhost:5110` (the default) |
| `NEXT_PUBLIC_API_URL` | **Not used.** Browser code always calls same-origin `/api/...` | Not used |

Rewrites are compiled into the build output, so `API_ORIGIN` must be set when
`next build` runs; changing it needs a rebuild.

### API

| Variable | Production value | Why |
| --- | --- | --- |
| `Cors__AllowedOrigins__0` | `https://app.example.com` | Browsers send `Origin` on same-origin POSTs too; the CSRF check (P1-10) requires it to be allowlisted |
| `ForwardedHeaders__KnownProxies__0` | The Next.js server's IP as the API sees it | Trust its `X-Forwarded-For`, so rate limiting (P1-08) counts real client IPs instead of one shared proxy IP |
| `App__FrontendBaseUrl` | `https://app.example.com` | Emailed confirm and reset links (P1-05) |
| `ConnectionStrings__DefaultConnection` | Production database | As before |
| Cookie domain | **None.** The cookie stays host-only | Nothing to set |

If the hop from Next.js to the API is plain HTTP, the API still marks the
cookie `Secure` (`SecurePolicy = Always`). It reads the original scheme from
the trusted `X-Forwarded-Proto`, so `UseHttpsRedirection` does not redirect
the internal hop.

## Local development

Local dev mirrors production: the frontend on `http://localhost:3000` uses the
same rewrite, with `API_ORIGIN` defaulting to `http://localhost:5110`. The
browser calls `http://localhost:3000/api/...`, so it also gets a host-only
cookie on `localhost`. Browsers accept `Secure` cookies on `http://localhost`.

`Cors:AllowedOrigins` keeps `http://localhost:3000` in
`appsettings.Development.json`. CORS itself is then unused by the frontend,
but the CSRF Origin check still needs it, and tools that call the API
directly from the browser keep working.

## Consequences

- P9-03 builds the client on relative `/api` paths and sends no API URL to
  the browser. `NEXT_PUBLIC_API_URL` is dropped from its `.env.example`.
- Every API request in production passes through the Next.js server, which
  adds one network hop. Acceptable for this app's traffic.
- The API must trust only the Next.js server's IP in
  `ForwardedHeaders:KnownProxies`. Trusting a wider range would let clients
  fake their IP to get around rate limiting.
- If the API is ever exposed directly to browsers (for example a mobile app or
  a public API), revisit this decision; the cookie and CSRF design assumes one
  browser origin.
