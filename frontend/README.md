This is a [Next.js](https://nextjs.org) project bootstrapped with [`create-next-app`](https://nextjs.org/docs/app/api-reference/cli/create-next-app).

## Getting Started

First, run the development server:

```bash
npm run dev
# or
yarn dev
# or
pnpm dev
# or
bun dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the result.

You can start editing the page by modifying `app/page.tsx`. The page auto-updates as you edit the file.

This project uses [`next/font`](https://nextjs.org/docs/app/building-your-application/optimizing/fonts) to automatically optimize and load [Geist](https://vercel.com/font), a new font family for Vercel.

## Learn More

To learn more about Next.js, take a look at the following resources:

- [Next.js Documentation](https://nextjs.org/docs) - learn about Next.js features and API.
- [Learn Next.js](https://nextjs.org/learn) - an interactive Next.js tutorial.

You can check out [the Next.js GitHub repository](https://github.com/vercel/next.js) - your feedback and contributions are welcome!

## Deploy on Vercel

The easiest way to deploy your Next.js app is to use the [Vercel Platform](https://vercel.com/new?utm_medium=default-template&filter=next.js&utm_source=create-next-app&utm_campaign=create-next-app-readme) from the creators of Next.js.

Check out our [Next.js deployment documentation](https://nextjs.org/docs/app/building-your-application/deploying) for more details.

## Lint (required by CI)

`npm run lint` must pass with no errors; the CI `frontend` check runs it on every Pull Request. Fix lint problems instead of disabling rules.

## Calling the API

The browser calls same-origin `/api/...` paths; `next.config.ts` rewrites them to `API_ORIGIN` (default `http://localhost:5110`, see `.env.example` and [ADR 0001](../docs/decisions/0001-same-origin-api-proxy.md)). The auth cookie is httpOnly: no code reads or stores a token.

- **Client components:** `useApiFetch()` from `lib/use-api-fetch.ts`. A 401 from a protected call redirects to `/login?next=<current page>`. `apiFetch()` from `lib/api.ts` is the same call without the redirect.
- **Server components and actions:** `apiFetchServer()` from `lib/api-server.ts` calls `API_ORIGIN` directly and forwards the incoming cookie.
- All three add the `X-Requested-With: Lorebound` header the API's CSRF check requires, send `json` as the JSON body, return `undefined` for empty responses, and throw `ApiError { status, title, detail, errors }` built from the problem JSON. `errors` holds the per-field validation messages.
- Types for every request and response DTO live in `lib/types.ts`. The mock-era `lib/settings/types.ts` and `lib/characters/types.ts` go away as each page is wired to the API (P9-07 to P9-14); new code must not import them.

```ts
const fetchApi = useApiFetch();
const setting = await fetchApi<SettingDetailDto>("/api/settings", {
  method: "POST",
  json: { name, description } satisfies CreateSettingRequest,
});
```

## Sign-in and the session

- `/login` and `/register` live in the `app/(auth)` route group, which has no sidebar. Both read `next` (same-site paths only, see `safeNextPath` in `lib/auth.ts`) and go there after signing in, or to `/dashboard`.
- Register signs in straight away. If the API refuses because the email is not confirmed yet (`Auth:RequireConfirmedEmail`), the page says "check your email" instead.
- `useSession()` from `lib/session.tsx` gives `status` (`loading`, `authenticated` with `user`, or `unauthenticated`), `refresh()` and `logout()`. `SessionProvider` in the root layout loads the user once from `GET /api/users/me`; call `refresh()` after anything that signs in.
- `proxy.ts` sends `/dashboard`, `/settings`, `/characters` and `/builder` to `/login?next=...` when there is no `lorebound.auth` cookie. This is only an optimistic check: `AppShell` also redirects when the session turns out to be signed out (an expired cookie, or Log out), and the API checks every request.
- The sidebar and mobile navigation show the user's display name, initials and email, and a Log out button (`components/layout/UserMenu.tsx`).

## Reading localStorage in pages

Until pages are wired to the API (Phase 9), settings and entries live in `localStorage` (`lib/settings/*-store.ts`). Do **not** load them with `setState` inside `useEffect`; the `react-hooks/set-state-in-effect` lint rule rejects it. Instead:

- Use `useIsClient()` from `lib/use-is-client.ts`, which is `false` during server rendering and hydration and `true` in the browser.
- Key the page content on it so it remounts once on the client, and read the store in a lazy `useState(() => isClient ? getSettings() : [])` initializer. See `app/settings/page.tsx`.
- To reset a form from props, give the form a `key` (see `components/settings/SettingEntryDialog.tsx`) rather than syncing state in an effect.
