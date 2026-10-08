# Lorebound API Roadmap

Single source of truth for building the Lorebound backend (auth, sharing, settings, characters, builder) and wiring the frontend to it. Every piece of work is a GitHub issue on this board.

**Repo:** [CSE499-Lorebound-Team/CharacterBackgroundGenerator](https://github.com/CSE499-Lorebound-Team/CharacterBackgroundGenerator) | **Integration branch:** `dev` | **Team workflow:** see the repository README

---

## How to use this board

| I want to... | Go to view |
|---|---|
| Pick up work | **Backlog** (filter Status = Ready, sort by Priority Level) |
| See what everyone is doing | **Board** |
| See only my work | **My Work** |
| Review PRs | **In Review** |
| Find stuck work | **Blocked** and **Needs Decision** |
| See phase progress | **Epics** and **Roadmap** |
| See the critical path | **Critical Path (P0)** |

## Status definitions

| Status | Meaning | Set by |
|---|---|---|
| Backlog | Not ready; dependencies open or unrefined | Auto-add |
| Ready | All "Blocked by" issues are done; can be picked up | Whoever unblocks it |
| In Progress | Someone is actively coding (branch exists) | Assignee |
| In Review | Pull request open | Automation (PR linked) |
| Blocked | Cannot proceed; comment explains why and add label `blocked` | Assignee |
| Done | Merged into `dev` and issue closed | Automation |

WIP limit: **max 2 items In Progress per person**. Finish or unblock before starting new work.

## Fields and labels

| Field | Values |
|---|---|
| **Work Type** | Epic, Feature, Task, Bug, Spike, Test, Docs, Chore (mirrors `type:` labels; named "Work Type" because "Type" is reserved by GitHub) |
| **Priority Level** | P0 Critical (critical path), P1 High, P2 Medium, P3 Low |
| **Size** | XS (about 1h), S (half a day), M (about 1 day), L (epics only; anything else this big must be split) |
| **Area** | API, Auth, Database, Sharing, Characters, Builder, Frontend, DevEx, Docs, Testing, Security |
| **Milestone** | The phase (Phase 0 to Phase 9) |
| Labels | `blocked`, `needs-decision`, `needs-repro` (plus `type:` and `area:` labels) |

**Issue types:** `feature` new endpoint or capability; `task` implementation unit; `bug` defect or suspected defect (write the failing test first); `spike` time-boxed decision/investigation with a written outcome; `test` automated test suite; `docs`; `chore`.

## Phases

| Phase | Epic goal | Issues |
|---|---|---|
| 0 Foundations | Identity-ready model, timestamps, conventions, DB config, migration, tests, CI, branch rules, onboarding script, lint fixes | 16 |
| 1 Authentication | Cookie-only auth, register/login/logout, email flows, rate limits, CSRF, `/users/me` | 13 |
| 2 Settings CRUD | Membership model, access helper, settings CRUD, cascade fix | 9 |
| 3 Sharing | Invite codes, accept, members, leave/remove | 8 |
| 4 Setting Entries | Entries CRUD, `IsGmOnly` secrecy, search | 8 |
| 5 Entry Relationships | Typed links, constraints, vocabulary decision | 6 |
| 6 Characters | Character models, CRUD, read-only rule, account deletion | 12 |
| 7 Builder Support | Step catalog, narrowed options, choices, complete | 6 |
| 8 Dashboard and Polish | Dashboard, seed data, OpenAPI, perf, security review | 7 |
| 9 Frontend Integration | Auth pages, API client, replace mocks, e2e | 17 |

Total: **102 work items + 10 epics = 112 issues.**

## Critical path

`P0-02` Identity user, `P0-08` migration, `P1-01` cookie auth, `P1-09` fallback policy and `ICurrentUser`, `P2-01` membership, `P2-02` access helper, then settings (`P2-03`+), entries (`P4-*`), characters (`P6-*`), builder (`P7-*`). Frontend work (`P9-*`) can start after `P1-12` (topology decision) and `P1-02/03`, and proceeds page by page as each API area lands.

## Design decisions baked into the issues

1. **Cookie auth only.** httpOnly cookie; no access, refresh, or bearer tokens anywhere, and none readable by JavaScript. `MapIdentityApi` is intentionally not used.
2. **Two roles**, GameMaster and Player. The creator is the owner; only the owner deletes a setting or changes roles; no co-owners.
3. **`IsGmOnly`** on entries hides secret lore from players everywhere.
4. **Removed players keep their characters, read-only.**
5. **Account deletion is blocked** while the user owns a setting that has other members.
6. **Backstory is free text only.** No generated or AI-written backstory.
7. **Production must be same-site** so the cookie is sent. Decided in `P1-12`: the frontend proxies `/api/*` to the API, so the browser sees one origin ([ADR 0001](../decisions/0001-same-origin-api-proxy.md)).

## Working agreement

1. Pick a **Ready** issue, assign yourself, move it to **In Progress**.
2. Branch from an up-to-date `dev`: `feature/<issue#>-short-slug` (`fix/` for bugs).
3. Keep the PR to one issue. Put `Closes #<issue>` in the PR description.
4. CI (`api` and `frontend`) must pass and review conversations must be resolved; branch protection on `main` and `dev` enforces both (P0-14). A teammate review is encouraged but not required.
5. Merge into `dev` (never `main`). The default branch stays `main` (decided in P0-12), so `Closes #N` links the PR but does **not** close the issue. After merging, close each issue by hand with a comment naming the PR and commits, so the card moves to **Done**.
6. Migrations: one per PR, announce schema changes to the team before merging.
7. Found something new? Open an issue from the forms (Task, Bug, Feature); it is auto-added to this board.

## Definition of Ready / Done

**Ready:** clear acceptance criteria, all "Blocked by" issues closed, size S or M.

**Done:** acceptance criteria met, tests written and green in CI, permission matrix tested (anonymous 401, non-member 404, player 403 on writes), docs updated, merged into `dev`.

## How issues are linked

- Each issue belongs to a **milestone** (its phase) and shows a parent **epic**.
- Epics list children as a task list and (best effort) as native **sub-issues**.
- **Blocked by** links appear in each issue's Dependencies section and (best effort) as native issue dependencies.
- PRs link to issues with `Closes #N` (issues are then closed by hand after the merge into `dev`; see Working agreement step 5).
- The **Auto-add** workflow puts every new issue and PR on this board.

## Maintainers

Issue definitions live in `docs/project-board/issues/`. The setup script `docs/project-board/scripts/setup-github.mjs` creates labels, milestones, issues, links, and board fields; it is safe to re-run. Board views and workflows are configured by hand per `docs/project-board/PROJECT-SETTINGS.md`.
