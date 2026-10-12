@@@ P9-01
title: Replace third-party "cn" package with clsx + tailwind-merge
type: bug
area: frontend
phase: 9
priority: P2
size: XS
labels: needs-repro
depends:
@@@
## Summary
`frontend/lib/utils.ts` is `export { cn } from "cn"`, and every shadcn component imports `cn` from the npm package `cn`. The standard shadcn helper is `twMerge(clsx(...))`. The third-party package is an unpinned, unrelated dependency that may not merge conflicting Tailwind classes.

## Steps to reproduce
1. Render `<Button className="px-8">`; inspect whether both the default `px-*` and `px-8` remain in the class list.

## Tasks
- [ ] `npm i clsx tailwind-merge` and `npm rm cn`.
- [ ] Implement `cn(...inputs) => twMerge(clsx(inputs))` in `lib/utils.ts`.
- [ ] Update component imports (`components/ui/*`) to `@/lib/utils`.
- [ ] Run `npm run lint` and `npm run build`.

## Acceptance criteria
- `cn` conflicts resolve to the last class; build and lint pass.

@@@ P9-02
title: Clean up frontend dependencies (shadcn devDependency, one icon library)
type: chore
area: frontend
phase: 9
priority: P3
size: XS
depends:
@@@
## Summary
`package.json` has `shadcn` (a CLI) in `dependencies`, and two icon libraries (`lucide-react` used widely, `@phosphor-icons/react` used only in `ui/dialog.tsx` and `ui/select.tsx`).

## Tasks
- [ ] Move `shadcn` to `devDependencies`.
- [ ] Replace the Phosphor icons in `dialog.tsx` and `select.tsx` with Lucide equivalents (`X`, `ChevronDown`, `Check`, `ChevronUp`); remove `@phosphor-icons/react`.
- [ ] Verify dialog and select visually.

## Acceptance criteria
- Only one icon library remains; build passes.

@@@ P9-03
title: API client with cookie credentials and ProblemDetails errors
type: task
area: frontend
phase: 9
priority: P0
size: M
depends: P1-12
@@@
## Summary
One typed fetch layer for all pages.

## Tasks
- [ ] `lib/api.ts` `apiFetch<T>(path, init)`: same-origin relative paths (`/api/...`; P1-12 chose the rewrite, see `docs/decisions/0001-same-origin-api-proxy.md`), `credentials: "include"`, header `X-Requested-With: Lorebound`, JSON in/out.
- [ ] `next.config.ts` `rewrites()` forwarding `/api/:path*` to `${API_ORIGIN}/api/:path*` (default `http://localhost:5110`), as in the ADR.
- [ ] Throw `ApiError { status, title, detail, errors }` parsed from problem JSON.
- [ ] Server-side variant that calls `API_ORIGIN` directly and forwards the incoming `cookie` header (server components need it; `cookies()` from `next/headers` is async in current Next).
- [ ] `.env.example` with `API_ORIGIN` (server-only; no `NEXT_PUBLIC_API_URL`).
- [ ] Shared DTO types in `lib/types.ts` mirroring API DTOs.
  - `SettingEntryType` matches the API enum: `Location | Culture | Religion | Faction | Organization | Profession | SocialClass | HistoricalEvent | Person | Other`. `Nation` and `City` become `Location`; retire `lib/settings/types.ts` and `lib/characters/types.ts`.
  - Entries have one `description: string | null` (no `summary`/`content`); `isGmOnly` is optional because the API omits it for players.
  - Setting role is `myRole: "GameMaster" | "Player"`, not `"GM"`.
  - Characters carry `isOwner`, `isReadOnly`, `homelandName` (list) and `choices: { stepKey, ordinal, entryId, entryName, entryType, freeText }[]`; `backstory` is nullable.
- [ ] Never read or store any token in JS; there are none.

## Implementation details
- Read `frontend/node_modules/next/dist/docs/` before coding (see `frontend/AGENTS.md`).
- On 401 from a protected call, redirect to `/login` with a `next` param.

## Acceptance criteria
- A test page can call `/api/health` and an authenticated endpoint after login.

@@@ P9-04
title: Login and register pages
type: feature
area: frontend
phase: 9
priority: P0
size: M
depends: P9-03, P1-02, P1-03
@@@
## Summary
Entry point for unauthenticated users.

## Tasks
- [ ] `/login` and `/register` pages using existing `Input`, `Label`, `Button`, `Card`.
- [ ] Client-side validation mirroring API rules (email, password length 10, display name).
- [ ] Show field errors from `ApiError.errors` and a generic banner for 401/429/lockout.
- [ ] After login, redirect to `next` or `/dashboard`; after register show "check your email" when confirmation is required.
- [ ] No auth layout chrome (no sidebar).

## Acceptance criteria
- Login sets the cookie; page never stores credentials.

@@@ P9-05
title: Session handling, route protection and real user in sidebar
type: feature
area: frontend
phase: 9
priority: P0
size: M
depends: P9-03, P1-11
@@@
## Summary
Replace the hardcoded "Joseph Marlow" and protect app routes.

## Tasks
- [ ] Fetch `/api/users/me` to build a session context (`useSession`).
- [ ] Route protection for `/dashboard`, `/settings`, `/characters`, `/builder`: use the Next 16 convention documented in `node_modules/next/dist/docs/` (may be `proxy.ts`), as an optimistic redirect only; the API remains the authority.
- [ ] Sidebar and `MobileNav` show real display name and initials; add a Log out action calling `POST /auth/logout`.
- [ ] Logged-in users visiting `/login` are redirected to `/dashboard`.

## Acceptance criteria
- Unauthenticated visit to `/dashboard` lands on `/login`; logout returns there.

@@@ P9-06
title: Confirm-email, forgot-password and reset-password pages
type: feature
area: frontend
phase: 9
priority: P1
size: S
depends: P9-04, P1-06, P1-07
@@@
## Summary
Frontend targets for the emailed links.

## Tasks
- [ ] `/confirm-email?userId=&code=` calls confirm and shows success/failure with a resend button.
- [ ] `/forgot-password` form (always shows the same success message).
- [ ] `/reset-password?email=&code=` form with new password.
- [ ] Link "Forgot password?" from login.

## Acceptance criteria
- Full email-confirmation and reset flow works with the dev console email sender.

@@@ P9-07
title: Settings list page and create-setting dialog wired to API
type: feature
area: frontend
phase: 9
priority: P0
size: M
depends: P9-05, P2-04, P2-03
@@@
## Summary
Replace mock settings on `/settings` (and the setting cards).

## Tasks
- [ ] Fetch `GET /api/settings` with search (debounced) and role filter (All / GM / Player).
- [ ] `SettingListCard` shows real `entryCount`, relative `updatedAt`, and a role badge.
- [ ] "Create Setting" opens a `Dialog` form posting to `/api/settings`; navigate to the new setting.
- [ ] Empty, loading, and error states.

## Acceptance criteria
- New setting appears without a page reload.

@@@ P9-08
title: Setting detail page - entries list, type tabs and search
type: feature
area: frontend
phase: 9
priority: P0
size: M
depends: P9-07, P4-02
@@@
## Summary
Replace hardcoded "Osepia" entries.

## Tasks
- [ ] Load `GET /api/settings/{id}` for header (name, role, counts).
- [ ] `SettingTabs` becomes functional: Overview (counts by type) plus one tab per `SettingEntryType`. Generate the tabs from the 10 API types (replaces the hardcoded Nations/Cities/Cultures/Religions/Factions); Overview counts come from `entryCountsByType`.
- [ ] `EntrySearch` filters via `?search=`; paging or "load more".
- [ ] `SettingEntryCard` shows real type, description, relationship count, and a "GM only" badge for GMs.
- [ ] `SettingEntryCard` and the entry detail page render `description` in place of `summary` and `content`.
- [ ] Hide GM-only actions from players.

## Acceptance criteria
- Tab switching and search hit the API; players never see hidden entries.

@@@ P9-09
title: Entry create, edit and delete dialogs
type: feature
area: frontend
phase: 9
priority: P1
size: M
depends: P9-08, P4-03, P4-05, P4-06
@@@
## Summary
Make "Add Entry" and "Edit Entry" real.

## Tasks
- [ ] Form: name, type `Select`, description `Textarea`, "GM only" switch.
- [ ] The type `Select` offers all 10 API entry types, so GMs can author the `Location`, `Profession` and `SocialClass` entries that builder steps 2, 5 and 6 draw on.
- [ ] One description `Textarea` replaces the separate summary and content fields in `SettingEntryDialog`; delete the empty `EntryDialog.tsx` if still unused.
- [ ] Create and edit through the same dialog component; validation errors displayed per field (including duplicate 409).
- [ ] Delete with confirmation; on 409 show relationship/character counts and offer "Delete anyway" (`force=true`).
- [ ] Refresh list after mutations.

## Acceptance criteria
- Full create, edit, delete cycle works for a GM.

@@@ P9-10
title: Entry relationships UI
type: feature
area: frontend
phase: 9
priority: P2
size: M
depends: P9-09, P5-04
@@@
## Summary
View and manage relationships from an entry.

## Tasks
- [ ] Entry detail panel/dialog lists incoming and outgoing relationships grouped by type.
- [ ] GM can add a relationship (entry picker, type input with suggestions from P5-05, description) and remove one.
- [ ] Players see read-only relationships.

## Acceptance criteria
- Adding a relationship updates the card's relationship count.

@@@ P9-11
title: Members, invites management and join page
type: feature
area: frontend
phase: 9
priority: P1
size: M
depends: P9-07, P3-05, P3-07
@@@
## Summary
Sharing UI for GMs and the join flow for players.

## Tasks
- [ ] "Members" tab on the setting page: list members, owner badge, GM actions (remove; owner: change role), "Leave setting" for players.
- [ ] Invites section (GM): create (expiry, max uses), copy join link, list with status, revoke.
- [ ] `/join/[code]`: requires login (redirect preserving the code), preview, "Join" button, redirect into the setting.
- [ ] Friendly messaging for invalid/expired codes (single generic message).

## Acceptance criteria
- A second user can join via link and immediately sees the setting as a Player.

@@@ P9-12
title: Characters list page wired to API
type: feature
area: frontend
phase: 9
priority: P0
size: M
depends: P9-05, P6-04
@@@
## Summary
Replace mock characters.

## Tasks
- [ ] Fetch `/api/characters` with debounced search and the All/Drafts/Complete filter buttons wired to `status`.
- [ ] `CharacterListCard` shows setting, homeland, status, and a "Read-only" badge when `isReadOnly`.
- [ ] "Create Character" flow: choose setting (from joined settings), create draft, go to builder.
- [ ] Delete with confirmation.
- [ ] Dashboard `QuickActions` links to the create-character flow instead of `/builder`; the builder needs a character (`/builder/{id}`, PR #208).

## Acceptance criteria
- Created draft appears in the list and opens in the builder.

@@@ P9-13
title: Builder shell - dynamic steps, options and summary panel
type: feature
area: frontend
phase: 9
priority: P0
size: M
depends: P9-12, P7-02
@@@
## Summary
Turn the hardcoded "step 2 of 8 / Choose Your Homeland" page into a data-driven wizard.

## Tasks
- [ ] Route `/builder/[characterId]`; load steps (`/builder/steps`), character detail, and current step options.
- [ ] Replace the hardcoded catalog in `lib/builder/steps.ts` and `lib/builder/types.ts` (PR #208) with `/api/builder/steps` (P7-01): step 1 `setting`, homeland from `Location`, step 5 `social_class`, step 6 `profession` (required), step 7 `motivation`. Drop the `faction` and `backstory` steps.
- [ ] `BuilderProgress` and `CharacterSummaryPanel` bind to real step and choice data (rows generated from the catalog plus `character.choices`: setting, homeland, culture, religion, social class, profession, motivation).
- [ ] Render `BuilderOptionCard` from options; show a "narrowed by your earlier choices" hint when `narrowed` is true.
- [ ] Free-text step (motivation) with textarea.
- [ ] Back/Next navigation with disabled states.

## Acceptance criteria
- Step content changes with the API catalog; no hardcoded option arrays remain.

@@@ P9-14
title: Builder save, resume, stale warnings and completion
type: feature
area: frontend
phase: 9
priority: P1
size: M
depends: P9-13, P7-05
@@@
## Summary
Persistence and finishing.

## Tasks
- [ ] Next saves via `PUT /choices/{key}` and advances; resume drafts at `currentStep`.
- [ ] Replace `lib/builder/builder-store.ts` (localStorage) with `PUT /choices/{key}` and `GET /characters/{id}`.
- [ ] Show stale-step warnings returned by the API and link to fix them.
- [ ] Review step lists all choices; "Complete" calls `/complete` and shows API validation errors.
- [ ] Read-only characters open in a view-only mode.
- [ ] Optional free-text backstory field saved via `PUT /characters/{id}`, on the Review step or the character page; it is not a catalog step (PR #208's step 7 editor moves here).

## Acceptance criteria
- A user can finish a character across sessions and see it as Complete.

@@@ P9-15
title: Dashboard wired to /api/dashboard
type: feature
area: frontend
phase: 9
priority: P1
size: S
depends: P9-07, P9-12, P8-01
@@@
## Summary
Remove dashboard mock data.

## Tasks
- [ ] Fetch `/api/dashboard` on the server (cookie forwarded).
- [ ] Bind `SettingCard`, `CharacterCard`, `RecentActivity`, and `QuickActions`.
- [ ] Wire "View All" buttons to `/settings` and `/characters`.

## Acceptance criteria
- Dashboard shows the logged-in user's real data.

@@@ P9-16
title: Loading, error and empty states; remove remaining mock data
type: task
area: frontend
phase: 9
priority: P1
size: S
depends: P9-15
@@@
## Summary
Finish the UX and delete leftovers.

## Tasks
- [ ] `loading.tsx` skeletons and `error.tsx` boundaries per route segment.
- [ ] Empty states with a call to action (no settings, no characters, no entries).
- [ ] Toast/inline feedback for mutations.
- [ ] Grep for leftover mock strings ("Osepia", "Theron Vale", "Joseph Marlow") and remove.

## Acceptance criteria
- No hardcoded mock data remains; every page handles the three states.

@@@ P9-17
title: Playwright e2e smoke test
type: test
area: testing
phase: 9
priority: P2
size: M
depends: P9-14
@@@
## Summary
Guard the whole loop.

## Tasks
- [ ] Add Playwright to `frontend/` with a config that boots API (seeded) and frontend.
- [ ] Scenario: register/login, create setting, add entries, invite second user, second user builds and completes a character.
- [ ] Optionally run in CI as a separate non-required job.

## Acceptance criteria
- Scenario passes locally with one command.
