# P8-04 query performance and index review

Reviewed 2026-10-12 against the API as of P8-03 (issue #121).

**Bottom line:**

- No endpoint has an N+1, and the query count is fixed per endpoint.
- One query scaled with the **whole database**, not the caller's data: dashboard activity. It is rewritten.
- Three indexes are added.
- Entry search stays a scan of one setting, which is fine at today's sizes. A threshold for revisiting it is below.

## 1. Queries per request

`api.Tests/Performance/QueryBudgetTests` counts the SQL commands each read endpoint runs, through `TestSupport/SqlCapture`, an EF Core command interceptor. It runs each endpoint twice: once with 1 row of everything and once with 12. Two checks:

- **No N+1:** the count is the same at 1 row and at 12.
- **A ceiling** for each endpoint, listed below.

Both run in CI, so a regression fails the build.

Every signed-in request starts with **2 identity queries**: the user, then their claims. They come from the security-stamp check that runs on every request (P1-01), which is what makes a password reset end other sessions immediately. That was a deliberate trade-off and is not changed here. The ceilings include those 2 queries.

| Endpoint | Queries | Ceiling |
|---|---|---|
| `GET /api/settings` | 4 | 4 |
| `GET /api/settings/{id}` | 6 (was 7) | 6 |
| `GET /api/settings/{id}/entries` (with or without `search`) | 5 | 5 |
| `GET /api/settings/{id}/entries/{entryId}` | 6 | 6 |
| `GET /api/settings/{id}/relationships` | 5 | 5 |
| `GET /api/settings/{id}/members` | 5 | 5 |
| `GET /api/settings/{id}/invites` | 5 | 5 |
| `GET /api/settings/{id}/characters` | 5 | 5 |
| `GET /api/characters` | 4 | 4 |
| `GET /api/characters/{id}` | 8 | 8 |
| `GET /api/characters/{id}/steps/{key}/options` | 6 | 6 |
| `GET /api/dashboard` | 10 | 10 |

**Review checklist:**

- **`AsNoTracking` and projection:** every read endpoint projects into its DTO in SQL, either with `Select` or with the shared `ToListItems` projections. The three `Include`s in the codebase are on write paths that load tracked entities (member role change, relationship update).
- **Cartesian explosion:** the entry detail (entry, outgoing and incoming relationships) already uses `AsSplitQuery`. No other read loads two collections at once.
- **Waste removed:** settings detail ran the access check twice, once through `RequireMemberAsync` and again through `GetRoleAsync`. A new `ISettingAccess.RequireMemberWithRoleAsync` returns both from one query. Settings update dropped its second lookup too: after `RequireGameMasterAsync` the role can only be GameMaster.

## 2. EXPLAIN ANALYZE at volume

### Data

[`p8-04-volume.sql`](p8-04-volume.sql) fills a throwaway database:

- 302 users;
- 550 settings;
- 79,600 entries, 10% GM-only;
- 10,000 relationships;
- 3,502 characters.

Osepia alone has 10,000 entries. The Player belongs to Osepia and 25 other settings.

500 of the settings belong to **other tenants** that the Player cannot see. This matters: a query that looks fine when the caller owns most of the data can still scan everyone else's.

### Method

[`p8-04-explain.sql`](p8-04-explain.sql) holds the exact SQL EF Core generates, copied from the Development SQL log. It runs as the Player, who also filters out GM-only entries, so it is the worst case. Machine: PostgreSQL 17.11 in Docker on a developer laptop.

- **Before:** one run before any change.
- **After:** the median of three runs on a database built by the real migrations.

At these sizes, runs vary by a few tenths of a millisecond.

| Query | Before | After | Change |
|---|---|---|---|
| Q1 entry search, count (`search=harbor`) | 16.4 ms | 8.5 ms | none (see decision 3) |
| Q2 entry search, first page | 17.4 ms | 1.6 ms | name index |
| Q3 entry list, first page | 12.6 ms | 0.6 ms | name index |
| Q4 entry list filtered by type | 0.1 ms | 0.6 ms | existing index, unchanged |
| Q5 dashboard, settings as GameMaster | 0.3 ms | 0.2 ms | none |
| Q6 dashboard, characters by status | 1.0 ms | 0.6 ms | owner index |
| Q7 dashboard, recent settings | 4.2 ms | 2.4 ms | none |
| Q8 dashboard, recent characters | 1.9 ms | 0.3 ms | owner index |
| **Q9 dashboard, recent entries** | **52.0 ms** | **0.8 ms** | **query rewrite plus index** |
| Q10 dashboard, recent characters (activity) | 1.3 ms | 0.03 ms | owner index |

Q4 (type filter) ranged from 0.4 to 1.6 ms across the three runs. It uses the existing `(CampaignSettingId, EntryType, Name)` index before and after, so the difference is run-to-run variation, not a regression.

### Findings

1. **Dashboard activity scaled with the whole database (fixed).**
   - **What happened:** the old query took every entry the caller can see, sorted them all by `UpdatedAt`, and then took 10. The plan sorted **the entire `SettingEntries` table**, spilling to disk ("external merge, 2.9 MB"), before it applied visibility.
   - **Why it mattered:** 500 other tenants' settings made it 4× slower without the caller's data changing at all.
   - **The fix:** `DashboardController` now asks for the newest 10 visible entries **per setting** the caller belongs to (an EF `SelectMany` with `Take`, which becomes a `JOIN LATERAL ... LIMIT 10`) and then takes the newest 10 of those. With the new `(CampaignSettingId, UpdatedAt)` index, each setting costs a backward index scan of at most 10 rows. The cost now depends only on how many settings the caller belongs to.

2. **The entry list sorted the whole setting for one page (fixed).**
   - **What happened:** `ORDER BY Name, Id LIMIT 20` sorted all 9,000 visible entries.
   - **The fix:** the new `(CampaignSettingId, Name, Id)` index is read in order and stops after 20 rows. That covers the list and the search page.

3. **Entry search's count still scans the setting (decision: no trigram index yet).**
   - **What happens:** `name ILIKE '%term%' OR description ILIKE '%term%'` can't use a B-tree. The count reads all of the setting's visible entries: 8.5 ms at 10,000.
   - **The usual fix:** a `pg_trgm` GIN index. `Name` is `citext`, though, so a trigram index would have to be built on `"Name"::text`, and the query would have to change to match it. That's real work for a query that is fast at any size the project expects.
   - **Revisit when** a single setting passes about **50,000 entries**, or search p95 passes **50 ms**.

4. **The owner check on "settings I belong to" uses `OR` (noted, not changed).**
   - **What happens:** `VisibleToCurrentUser` is `OwnerUserId = me OR EXISTS(membership)`. The `OR` keeps Postgres from using an index, so it scans `CampaignSettings`: 0.2 ms at 550 settings.
   - **Why the `OR` stays:** every owner has a GameMaster membership row (P2-01), so the `OR` could become a plain membership join. But it also protects the rare owner whose row is missing, and tests rely on that.
   - **Revisit when** there are about **100,000 settings**.

5. **An index alone was not enough for the dashboard.** `(CampaignSettingId, UpdatedAt)` on its own changed nothing: the old query shape still sorted the whole table. It only pays off together with the per-setting rewrite in finding 1. A trigram index was not tried, for the `citext` reason in finding 3.

## 3. What changed

**Migration `AddListIndexes`:**

| Index | Serves |
|---|---|
| `SettingEntries (CampaignSettingId, Name, Id)` | entry list and search pages |
| `SettingEntries (CampaignSettingId, UpdatedAt)` | dashboard activity, per setting |
| `Characters (OwnerUserId, UpdatedAt)` | Characters page and dashboard |

The last one **replaces** `IX_Characters_OwnerUserId`, because its leading column still serves the foreign key.

**Code:**

- `DashboardController`: per-setting activity query.
- `ISettingAccess.RequireMemberWithRoleAsync`: setting detail does one access query.
- `SettingsController.Update`: no second role lookup.

**Tests:** `Performance/QueryBudgetTests` (14 endpoint cases).

## 4. Rerunning the review

```powershell
docker exec lorebound-postgres psql -U lorebound -d postgres -c "CREATE DATABASE lorebound_perf OWNER lorebound"
cd api
dotnet ef database update --connection "Host=localhost;Port=5432;Database=lorebound_perf;Username=lorebound;Password=<your .env password>"
cd ..
Get-Content docs/performance/p8-04-volume.sql  | docker exec -i lorebound-postgres psql -U lorebound -d lorebound_perf
Get-Content docs/performance/p8-04-explain.sql | docker exec -i lorebound-postgres psql -U lorebound -d lorebound_perf
docker exec lorebound-postgres psql -U lorebound -d postgres -c "DROP DATABASE lorebound_perf"
```

To see the SQL any endpoint runs, start the API in Development. EF Core logs every command at Information, under `Microsoft.EntityFrameworkCore.Database.Command`, with the request's `TraceId`. Parameter values show as `?`.
