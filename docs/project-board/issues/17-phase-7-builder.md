@@@ P7-01
title: Builder step catalog and GET /api/builder/steps
type: feature
area: builder
phase: 7
priority: P0
size: S
depends: P6-01
@@@
## Summary
One server-side definition of the 8 builder steps so client and validation agree.

## Step catalog (confirmed 2026-10-09; the contract the frontend follows)
| # | key | title | source | required |
|---|-----|-------|--------|----------|
| 1 | setting | Setting | (chosen at character creation) | yes |
| 2 | homeland | Choose Your Homeland | entries of type Location | yes |
| 3 | culture | Culture | Culture | yes |
| 4 | religion | Religion | Religion | no |
| 5 | social_class | Social Class | SocialClass | no |
| 6 | profession | Profession | Profession | yes |
| 7 | motivation | Motivation | free text (max 2000) | yes |
| 8 | review | Review | none (UI only, not stored) | n/a |

## Tasks
- [ ] Static `BuilderSteps` record list: `Key, Order, Title, Description, EntryType?, AllowFreeText, Required, MaxSelections`.
- [ ] `GET /api/builder/steps` (authenticated) returns the catalog.
- [ ] Unit test asserting keys are unique and orders contiguous.
- [ ] Treat the catalog as the frontend contract: P9-13 replaces the hardcoded list in `frontend/lib/builder/steps.ts` (PR #208: `character`, `faction` and `backstory` steps, homeland from `Nation`/`City`) with this endpoint. Backstory stays on `PUT /api/characters/{id}` and is not a catalog step.

## Implementation details
- Adding a step later = add a catalog row; no migration (choices are rows).

## Acceptance criteria
- Endpoint returns 8 ordered steps matching the frontend's `totalSteps = 8`.
- The frontend reads step keys, titles and count from this endpoint; no client-side copy of the catalog remains after P9-13.

@@@ P7-02
title: GET /api/characters/{id}/steps/{key}/options (relationship-narrowed)
type: feature
area: builder
phase: 7
priority: P0
size: M
depends: P7-01, P5-05, P5-02
@@@
## Summary
The core value of the relationship graph: options for a step narrowed by earlier choices.

## Tasks
- [ ] Requires character write access.
- [ ] Candidates = entries of the step's `EntryType` in the character's setting, visible to the caller's role (players never get GM-only entries).
- [ ] Take entries chosen in earlier steps; find candidates linked to any of them by a relationship (either direction, per the P5-05 ADR).
- [ ] If at least one candidate is linked, return only linked candidates with `narrowed: true`; otherwise return all candidates with `narrowed: false` (avoid dead ends).
- [ ] Response: `{ stepKey, narrowed, options: [{ entryId, name, description }] }`.
- [ ] Free-text steps return an empty options list.

## Acceptance criteria
- Choosing a homeland linked to two cultures returns only those cultures.

@@@ P7-03
title: PUT /api/characters/{id}/choices/{key}
type: feature
area: builder
phase: 7
priority: P0
size: M
depends: P7-01, P6-06
@@@
## Summary
Validated saving of one step's answer.

## Tasks
- [ ] Body: `{ entryIds?: Guid[], freeText?: string }`.
- [ ] Validate: step exists; entries belong to the character's **same setting**; entry type matches the step; entries visible to the caller (or already chosen); selection count within `MaxSelections`; free-text only when allowed; length limits.
- [ ] Replace that step's choices atomically.
- [ ] Set `CurrentStep = max(CurrentStep, order + 1)`.
- [ ] Return the updated `CharacterDetailDto`.

## Implementation details
- Requires write access (403 for read-only characters, per P6-10).

## Acceptance criteria
- Invalid type, cross-setting, or hidden entry is rejected with a 400 problem detail.

@@@ P7-04
title: Detect stale later choices when an earlier step changes
type: feature
area: builder
phase: 7
priority: P2
size: S
depends: P7-02, P7-03
@@@
## Summary
Changing homeland can make an already-chosen culture inconsistent. Report it; never silently delete.

## Tasks
- [ ] After saving a choice, recompute options for each later step that has a choice.
- [ ] If narrowing applied and the saved entry is no longer among the options, include its step key in `staleSteps` in the response.
- [ ] `GET /api/characters/{id}` also returns `staleSteps`.
- [ ] `POST /complete` (P7-05) refuses while stale steps exist.

## Acceptance criteria
- Switching homeland flags the culture step as stale without deleting the choice.

@@@ P7-05
title: Complete and reopen a character
type: feature
area: builder
phase: 7
priority: P1
size: S
depends: P7-03
@@@
## Summary
Move between Draft and Complete.

## Tasks
- [ ] `POST /api/characters/{id}/complete`: every required step has a choice, entries still exist and belong to the setting, no stale steps, name is not empty; then `Status = Complete`. Otherwise 400 listing missing/invalid steps.
- [ ] `POST /api/characters/{id}/reopen`: `Status = Draft`.
- [ ] Both require write access.

## Acceptance criteria
- Incomplete character cannot complete; reopen edits allowed again.

@@@ P7-06
title: Builder flow integration tests
type: test
area: testing
phase: 7
priority: P0
size: M
depends: P7-02, P7-03, P7-04, P7-05
@@@
## Summary
End-to-end proof of the guided flow.

## Tasks
- [ ] Happy path through all required steps to Complete.
- [ ] Narrowing with and without linked candidates.
- [ ] Rejection cases: wrong type, cross-setting entry, hidden entry, too many selections.
- [ ] Stale detection after changing an earlier choice.
- [ ] Read-only character cannot save choices.

## Acceptance criteria
- Tests pass in CI.
