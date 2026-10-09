# Lorebound API conventions

Every endpoint follows these rules so the frontend sees one consistent contract.

## JSON

- Property names are camelCase (the ASP.NET Core default).
- Enums are sent and received as their names, e.g. `"entryType": "Location"`,
  never `0`. `JsonStringEnumConverter` is registered globally in `Program.cs`.
- Timestamps are `DateTimeOffset` in UTC (ISO 8601 with offset).

## Database

- Every enum column is stored as a string. `LoreboundDbContext.ConfigureConventions`
  applies this to all enum properties, so new enums (`SettingRole`,
  `CharacterStatus`, ...) need no extra configuration.
- Schema changes ship as EF Core migrations in `Data/Migrations/`; see
  "Migrations" in the root README for commands and rules.
- User emails are unique at the database level (`EmailIndex` on
  `NormalizedEmail`), not only through Identity's app-level check.
- Entities implementing `ITimestamped` get `CreatedAt`/`UpdatedAt` set on save.
  `ExecuteUpdate`/`ExecuteDelete` bypass this, so set `UpdatedAt` yourself.
- Access to a setting is a `SettingMembership` row (`GameMaster` or `Player`),
  one per (setting, user), enforced by a unique index. `JoinedAt` defaults to
  the save time. The owner (`CampaignSetting.OwnerUserId`) **always** has a
  `GameMaster` row too, so create both together and never remove or demote the
  owner's row. Deleting a setting deletes its memberships; a user who still
  has memberships cannot be deleted.
- A `SettingInvite` is a 10-character Crockford base32 code (`Sharing/InviteCodes.cs`,
  no `I L O U`), unique across all settings. Accepting one always grants
  `Player`. Codes are stored in plain text so a GM can re-share them; they are
  unguessable and revocable (`RevokedAt`; rows are never deleted while the
  setting exists). Check constraints keep `MaxUses` positive and `UseCount`
  between 0 and `MaxUses`. Deleting a setting deletes its invites; a user who
  created invites cannot be deleted.
- A `SettingEntry` name is `citext` (the `citext` extension is enabled by
  migration), unique per (setting, type) **ignoring case**: "Sharn" and
  "SHARN" cannot both be Locations of one setting, but may be a Location and
  a Faction. `IsGmOnly` (default `false`) marks secret lore. **Every query
  that reads entries for a response goes through
  `SettingEntryQueries.VisibleTo(role)`** (`Data/SettingEntryQueries.cs`),
  which drops `IsGmOnly` entries for Players; this includes counts (the
  settings list `entryCount` and detail `entryCountsByType`).
- A `SettingEntryRelationship` is a directed, typed link between two entries.
  The database rejects a self-link (check constraint
  `CK_SettingEntryRelationships_NoSelfLink`) and a second link with the same
  source, target and type (unique index
  `IX_SettingEntryRelationships_Source_Target_Type`); another type or the
  reverse direction is a different link. `RelationshipType` is `citext`, so
  that index ignores case (P5-05), and at most 60 characters (check
  constraint `CK_SettingEntryRelationships_RelationshipTypeLength`);
  `Description` is at most 1000. The database cannot check that
  both entries belong to the relationship's setting, so the create endpoint
  (P5-03) does. Relationship reads go through the
  `VisibleTo(role)` overload for relationships, which hides from Players any
  link whose source **or** target is GM-only.
- A `Character` belongs to one setting and one owner. New ones are
  `Status = Draft` (`Draft`/`Complete`, stored as names), `CurrentStep = 1`,
  named "Unnamed Character"; `Name` is at most 100 characters and
  `Backstory` (free text only, nothing generates it) at most 10000.
  Deleting a setting deletes its characters; a user who owns characters
  cannot be deleted (account deletion removes them first, P6-11).
- Builder answers are `CharacterChoice` rows, not columns, so a new builder
  step needs no migration: one per (character, `StepKey`, `Ordinal`)
  (unique index), each either an `EntryId` or `FreeText` (at most 2000),
  **never both** (check constraint `CK_CharacterChoices_EntryOrFreeText`).
  Deleting a character deletes its choices. Deleting an entry sets the
  choice's `EntryId` to null and keeps the character, so a choice may end
  up with neither; that is why the database allows "neither", and saving a
  choice (P7-03) must refuse it.

## DTOs

- Controllers never return entities. Request and response shapes live in
  `Dtos/<Resource>/` as `record` types, e.g. `Dtos/Entries/SettingEntryDto.cs`.
- Entities are mapped with hand-written extension methods in `Mapping/`,
  e.g. `entry.ToDto()`. No AutoMapper.
- On positional request records, put validation attributes on the parameter
  (`record CreateX([Required] string Name)`), not `[property: Required]`;
  MVC rejects the latter with a 500.

## Paging

- List endpoints take `[FromQuery] PageQuery` (`page` defaults to 1,
  `pageSize` defaults to 20 and is capped at 100; out-of-range values are clamped).
- They return `PagedResult<T>`:

  ```json
  { "items": [], "page": 1, "pageSize": 20, "totalCount": 0 }
  ```

## Errors

Every error is RFC 7807 `application/problem+json` with a `traceId` extension:

```json
{ "type": "...", "title": "Not Found", "status": 404, "detail": "Setting not found.", "traceId": "00-..." }
```

- Throw `NotFoundException` (404), `ForbiddenException` (403) or
  `ConflictException` (409) from `Errors/`; `ApiExceptionHandler` maps them.
  Their message becomes `detail`, so write it for API clients. A
  `ConflictException` may carry extra members for the client, e.g.
  `{ relationshipCount, characterCount }` when deleting a linked entry.
- Invalid request bodies return 400 `ValidationProblemDetails` with an
  `errors` dictionary keyed by field name.
- Any other exception returns a generic 500 with no exception details.
- Bodyless error statuses (e.g. unmatched routes) also return problem JSON.
  An unmatched route is 404 when signed in but 401 when anonymous, because
  the fallback authorization policy also covers requests with no endpoint.

## CORS

- Origins come from `Cors:AllowedOrigins` (a string array). Development
  allows `http://localhost:3000` via `appsettings.Development.json`; in
  production set `Cors__AllowedOrigins__0` (and `__1`, ...) as environment variables.
- In production the frontend proxies `/api/*` to the API (same origin, see
  `docs/decisions/0001-same-origin-api-proxy.md`), so browsers make no
  cross-origin calls; the origin list still matters because the CSRF check
  only accepts unsafe requests from these origins.
- Credentials are allowed so the auth cookie is sent, so the origin list is
  always explicit, never `*`.
- `AllowAnyHeader()` echoes requested headers (including `X-Requested-With`).
  Do not add `WithHeaders(...)` alongside it: that disables any-header and
  blocks `Content-Type`.

## Authentication

- ASP.NET Core Identity with a **cookie only** (`Auth/AuthenticationSetup.cs`).
  The API never returns bearer or refresh tokens, so never call
  `MapIdentityApi`/`AddIdentityApiEndpoints` (their `/login` can return tokens
  in the body; `AuthenticationTests` fails if those routes appear).
- Cookie `lorebound.auth`: `HttpOnly`, `Secure`, `SameSite=Lax`, 14-day sliding
  expiry when persistent, session cookie otherwise.
- **Secure by default:** a fallback policy requires a signed-in user on every
  endpoint. Mark public actions `[AllowAnonymous]` (minimal endpoints:
  `.AllowAnonymous()`). Today only `/api/health`, the anonymous `/api/auth`
  actions and the Development OpenAPI document are public;
  `FallbackPolicyTests` lists them, so adding one is a deliberate change.
  Never put `[AllowAnonymous]` on a controller class: it overrides
  `[Authorize]` on every action.
- Inject `ICurrentUser` (`UserId`, `Email`, `DisplayName`) to get the signed-in
  user; do not read claims directly. It throws `UnauthorizedAccessException`
  when nobody is signed in. The display name is a cookie claim added by
  `LoreboundClaimsPrincipalFactory` and refreshed on every request.
- Unauthenticated requests to protected endpoints get **401** problem JSON and
  forbidden ones **403**; the API never redirects to a login page.
- The cookie's security stamp is checked against the database on every request
  (Identity's default is every 30 minutes), so a password reset signs the user
  out everywhere at once; logout does the same. Tests on `ApiFactory` (no
  database) turn this off.
- Passwords: at least 10 characters, no forced digit/case/symbol rules. Emails
  are unique. Five failed sign-ins lock an account for 15 minutes.
- `Auth:RequireConfirmedEmail` controls whether sign-in needs a confirmed
  email: `false` in `appsettings.Development.json`, `true` everywhere else
  (and `true` if the key is missing).
- Emails are used as the Identity username, so any valid address is accepted
  (the default username character allowlist is turned off).

### Endpoints

| Endpoint | Success | Failures |
| --- | --- | --- |
| `POST /api/auth/register` `{ email, password, displayName }` | **201** `{ id, email, displayName, emailConfirmed }`; does not sign in. Always emails a confirmation link (`Auth:RequireConfirmedEmail` only decides whether login needs it) | **400** validation problem keyed by `Email`, `Password` or `DisplayName` (1-60 chars, trimmed). When confirmation is required, a taken email gets a generic "Could not register with these details." |
| `POST /api/auth/login` `{ email, password, rememberMe }` | **200** `{ id, email, displayName }` plus `Set-Cookie` | **401** "Invalid email or password." for a wrong password, an unknown email or an unconfirmed email alike; **423** when locked out, with `Retry-After` and `retryAfterSeconds` |
| `POST /api/auth/logout` (signed in) | **204**; signs out **everywhere**: the security stamp changes, so every copy of the cookie (other devices, a stolen copy) stops working, and `Set-Cookie` expires this browser's cookie (same name and path as at sign-in) | **401** when not signed in |
| `POST /api/auth/confirm-email` `{ userId, code }` | **204**; the email is confirmed | **400** "This confirmation link is invalid or has already been used." for an unknown user, a malformed or wrong code, or an already-confirmed account (so a code works once) |
| `POST /api/auth/resend-confirmation` `{ email }` | **204** always; a new link is sent only to an unconfirmed account | **400** only for a missing or malformed email |
| `POST /api/auth/forgot-password` `{ email }` | **204** always; a reset link is sent only to an existing account with a **confirmed** email | **400** only for a missing or malformed email |
| `POST /api/auth/reset-password` `{ email, code, newPassword }` | **204**; the password changes and the user's other sessions end | **400** "This reset link is invalid or has expired." for an unknown email, a malformed, wrong or used code; **400** keyed by `NewPassword` for a weak password |
| `GET /api/users/me` (signed in) | **200** `{ id, email, displayName, emailConfirmed, createdAt }` | **401** when not signed in |
| `PUT /api/users/me` `{ displayName }` (signed in) | **200** with the updated profile; the session stays valid and the display name claim refreshes on the next request | **400** keyed by `DisplayName` (1-60 chars, trimmed); **401** when not signed in. Email and password changes are out of scope for now |
| `DELETE /api/users/me` `{ password }` (signed in) | **204**; the account and everything it owns are deleted in one transaction, the cookie is cleared and every other session stops working | **400** keyed by `Password` when it is missing or wrong (rate limited like login); **409** with `settings: [{ id, name, otherMemberCount }]` while the user owns a setting that has other members, and nothing is deleted; **401** when not signed in |

- **Deleting an account (P6-11)** is blocked while the user owns a setting
  that has any other member. There is **no ownership transfer** (a known
  limitation): the owner must delete those settings, or remove their
  members, first. Otherwise one transaction deletes the settings the user
  owns with everything in them (entries, relationships, memberships,
  invites, and characters, including ones removed players left there), the
  user's memberships, characters and created invites in other settings,
  and then the user. Other people's data is never touched.

- `rememberMe: false` gives a session cookie; `true` gives the 14-day cookie.
- An unknown email still runs a password hash check, so the response time
  does not reveal which emails are registered.
- Never log passwords or emails; log the user id.

## Setting access

`ISettingAccess` (`Auth/SettingAccess.cs`) is the one place that decides what
the signed-in user may do in a setting. Every endpoint that reads or changes
setting data calls it **before** touching that data; `SettingAccessConventionTests`
fails if a controller routed under `api/settings` does not inject it.

| Method | Owner | GameMaster | Player | Non-member or missing setting |
| --- | --- | --- | --- | --- |
| `GetRoleAsync(id)` | `GameMaster` | `GameMaster` | `Player` | `null` |
| `RequireMemberAsync(id)` | setting | setting | setting | 404 |
| `RequireGameMasterAsync(id)` | setting | setting | 403 | 404 |
| `RequireOwnerAsync(id)` | setting | 403 | 403 | 404 |
| `VisibleToCurrentUser()` | included | included | included | excluded |

- A non-member gets **404**, not 403, so nobody can probe which setting ids
  exist. A member without the needed role gets **403**.
- The owner always counts as a GameMaster, even if their membership row were
  missing.
- Each check is one database query. The returned setting is tracked, so an
  endpoint can change it and call `SaveChangesAsync`.
- Use `VisibleToCurrentUser()` as the starting point of every settings list
  query, never `db.CampaignSettings` directly.

### Settings endpoints

Who gets what from each `/api/settings` endpoint. `SettingsAuthorizationTests`
and `SettingsListTests` assert every cell, so change them together.

| Endpoint | Anonymous | Non-member | Player | GameMaster | Owner |
| --- | --- | --- | --- | --- | --- |
| `POST /api/settings` | 401 | **201**, becomes owner and GameMaster | n/a | n/a | n/a |
| `GET /api/settings` | 401 | 200, setting not listed | 200, listed | 200, listed | 200, listed |
| `GET /api/settings/{id}` | 401 | 404 | 200 | 200 | 200 |
| `PUT /api/settings/{id}` | 401 | 404 | 403 | 200 | 200 |
| `DELETE /api/settings/{id}` | 401 | 404 | 403 | 403 | **204** |

- An unknown id is 404 for everyone, with the same body a non-member gets.
- `GET /api/settings?role=gm` lists settings where I am owner or GameMaster;
  `role=player` those where I am a Player; any other value is 400.
- Deleting a setting removes its entries, relationships and memberships.

### Invite endpoints

A GameMaster shares a setting by creating an invite code; whoever accepts it
joins as a **Player**. Only GameMasters see or manage codes.
`InviteCreateTests` and `InviteManagementTests` assert this table.

| Endpoint | Anonymous | Non-member | Player | GameMaster | Owner |
| --- | --- | --- | --- | --- | --- |
| `POST /api/settings/{sid}/invites` | 401 | 404 | 403 | **201** | **201** |
| `GET /api/settings/{sid}/invites` | 401 | 404 | 403 | 200 | 200 |
| `DELETE /api/settings/{sid}/invites/{id}` | 401 | 404 | 403 | **204** | **204** |

- Body (optional) `{ expiresInDays?, maxUses? }`: `expiresInDays` 1-90,
  default 7; `maxUses` 1-100, default unlimited. Out of range is 400 keyed by
  the field.
- Response `InviteDto`: `{ id, code, joinUrl, status, expiresAt, maxUses,
  useCount, revokedAt, createdAt }`. `joinUrl` is
  `{App:FrontendBaseUrl}/join/{code}`, built by `FrontendLinks.JoinSetting`.
- `status` is `Active`, `Expired`, `Revoked` or `Exhausted`, computed by
  `InviteRules.StatusAt` (revoked wins over expired, which wins over exhausted).
  `InviteRules.IsActiveAt` is the same rule as a query filter; use these two
  rather than re-deriving validity.
- A setting may have at most **20 active** invites (not revoked, not expired,
  uses left); the 21st is **409**. It is a soft cap: two simultaneous
  creates can both pass it.
- A code collision on insert is retried with a new code (up to 3 attempts).
- `GET` returns `PagedResult<InviteDto>` of every invite (all statuses),
  newest first.
- `DELETE` is a soft revoke: it sets `revokedAt` and keeps the row, so the
  invite stays listed as `Revoked`. Revoking again is a no-op 204 that keeps
  the first `revokedAt`. An invite id from another setting is 404.

### Joining by code

The joining side works by code, not setting id (`InvitesController`, routes
under `api/invites`). `InvitePreviewTests` and `InviteAcceptTests` assert
these rules.

| Endpoint | Anonymous | Any signed-in user, usable code | Unusable code |
| --- | --- | --- | --- |
| `GET /api/invites/{code}` | 401 | 200 `{ settingName, gmDisplayName, alreadyMember }` | 404 |
| `POST /api/invites/{code}/accept` | 401 | 200 `{ settingId, myRole }` | 404 |

- **Every unusable code gets the identical 404** ("Invite not found."):
  unknown, malformed, expired, revoked or used up, so a caller cannot tell a
  dead code from one that never existed. Never add a different status or
  message for one of these cases. The one exception is accepting a code of a
  setting you already belong to (below).
- Codes are read leniently by `InviteCodes.TryNormalize`: case-insensitive,
  hyphens and spaces ignored, `I`/`L` read as `1` and `O` as `0`. Anything
  that still cannot be a code is 404 without a database lookup.
- `gmDisplayName` is the owner's display name. `alreadyMember` is true for
  the owner and any member. Emails are never returned.
- Previewing never changes the invite (`useCount` stays the same).
- Accepting adds a `Player` membership and adds 1 to `useCount`, in one
  transaction. A user who already belongs (owner, GameMaster or Player) gets
  200 with their current role and uses nothing, so retries are safe.
  Membership is checked **before** validity, so retrying an accept that took
  the last use is still 200, not 404. This tells a member nothing they could
  not already see; preview stays strict (404 for any unusable code).
- Race-safe: the use is taken by one `UPDATE ... WHERE` the invite is still
  usable, so parallel accepts of a last use admit exactly one user and the
  rest get 404. A second parallel accept by the same user hits the unique
  membership index before taking a use and is answered as already-member.
  The `CK_SettingInvites_UseCount` check is the last line of defence.
- Rate limited by the `invites` policy (see Rate limiting).

### Members

`SettingMembersController`; `SettingMembersTests` asserts this table and
`MemberRemovalTests` the removal one below.

| Endpoint | Anonymous | Non-member | Player | GameMaster | Owner |
| --- | --- | --- | --- | --- | --- |
| `GET /api/settings/{sid}/members` | 401 | 404 | 200 | 200 | 200 |
| `PATCH /api/settings/{sid}/members/{userId}` `{ role }` | 401 | 404 | 403 | 403 | **200** |
| `DELETE /api/settings/{sid}/members/{userId}` | 401 | 404 | leave only | leave, remove Players | remove anyone but self |

- `GET` returns `PagedResult<MemberDto>`, each `{ userId, displayName, role,
  isOwner, joinedAt }`: the owner first, then GameMasters, then Players,
  each oldest first. **Emails are never exposed.**
- Only the **owner** changes roles, so there are no co-owners. Other
  GameMasters manage invites and remove Players (P3-07) but cannot promote
  or demote.
- `PATCH` returns the updated `MemberDto`. Setting the role a member already
  has is a no-op 200. The owner cannot be demoted (**409**); a `userId` that
  is not a member of this setting is 404; a missing or unknown role
  (`GameMaster` and `Player` only) is 400.

#### Removing a member or leaving

`DELETE /api/settings/{sid}/members/{userId}` does both: with your own
`userId` you leave, otherwise you remove someone. **204** on success.
`MemberRemovalTests` asserts this table (rows: who calls; columns: whom).

| Caller \ target | Self (leave) | Player | GameMaster | Owner |
| --- | --- | --- | --- | --- |
| Player | **204** | 403 | 403 | 403 |
| GameMaster | **204** | **204** | 403 | 403 |
| Owner | 409 | **204** | **204** | n/a |

- Anonymous is 401 and a non-member caller 404, as everywhere.
- The **owner can never leave or be removed**; leaving answers 409 "Delete
  the setting instead".
- A Player is refused (403) before the target is looked up, so they cannot
  probe who belongs. For a GameMaster or the owner, a `userId` that is not a
  member is 404 (so removing twice is 404 the second time).
- Removal is immediate: the user's next request gets 404 on the setting and
  it drops out of their settings list. Their account and any invites they
  created stay, and they can rejoin with a new invite. Characters are not
  deleted; P6-10 makes them read-only.

### Entry endpoints

A setting's lore (`SettingEntriesController`, routes under
`api/settings/{sid}/entries`). Any member reads; GameMasters write.
**GM-only entries never reach a Player**: not in lists, search, counts or
another entry's relationships.

| Endpoint | Anonymous | Non-member | Player | GameMaster | Owner |
| --- | --- | --- | --- | --- | --- |
| `GET /api/settings/{sid}/entries` | 401 | 404 | 200, no GM-only | 200 | 200 |
| `POST /api/settings/{sid}/entries` | 401 | 404 | 403 | **201** | **201** |
| `GET /api/settings/{sid}/entries/{id}` | 401 | 404 | 200; 404 if GM-only | 200 | 200 |
| `PUT /api/settings/{sid}/entries/{id}` | 401 | 404 | 403 | 200 | 200 |
| `DELETE /api/settings/{sid}/entries/{id}` | 401 | 404 | 403 | **204**; 409 if linked | **204**; 409 if linked |

- `GET` takes `type` (an entry type name, e.g. `Location`; unknown is 400),
  `search` (case-insensitive substring of name or description; `%` and `_`
  match literally), `gmOnly` (`true`/`false`) and `page`/`pageSize`. Sorted
  by name, ignoring case.
- Returns `PagedResult<EntryListItemDto>`, each `{ id, name, entryType,
  description, isGmOnly, relationshipCount, updatedAt }`. `isGmOnly` is
  **only sent to GameMasters**; a Player's items have no such property, and
  `gmOnly=true` gives a Player an empty page.
- `relationshipCount` counts relationships in both directions whose **other**
  entry the caller can see.
- `POST` body `{ name, entryType, description?, isGmOnly? }`: `name` 1-120
  characters (trimmed), `entryType` a defined type name, `description` up to
  4000 (blank becomes `null`), `isGmOnly` defaults to `false`. Invalid is 400
  keyed by the field. A name already used by an entry of the **same type**
  in the setting, ignoring case, is **409** (the unique index also catches
  concurrent creates). Returns **201** `SettingEntryDto` `{ id,
  campaignSettingId, name, description, entryType, isGmOnly, createdAt,
  updatedAt }` with a `Location` header.
- `GET .../entries/{id}` returns `EntryDetailDto`: the entry's fields plus
  `outgoing` and `incoming` lists of `{ id, otherEntryId, otherEntryName,
  otherEntryType, relationshipType, description }` (the other entry is the
  target for outgoing, the source for incoming), sorted by the other entry's
  name. A Player asking for a GM-only entry gets the **same 404** as for a
  missing one ("Entry not found."), and relationships whose other entry is
  GM-only are left out, so a Player cannot learn a hidden entry exists. An
  entry id from another setting is 404.
- `PUT .../entries/{id}` replaces every field with the same body and rules
  as `POST` (so omitting `isGmOnly` makes the entry visible). The type may
  change and secrecy may toggle. The duplicate-name check ignores the entry
  itself, so changing only the case of its name is fine. Returns 200
  `SettingEntryDto` with a new `updatedAt`. Making an entry GM-only does
  **not** change characters that already chose it (P6-05).
- `DELETE .../entries/{id}` of an entry that has relationships (either
  direction) **or that characters have chosen** (P6-09) is **409** with
  `relationshipCount` and `characterCount` members in the problem JSON
  (`characterCount` counts characters, not choices, of every owner), and
  nothing changes. With `?force=true` its relationships and the entry are
  deleted in one transaction (other relationships are kept), and the
  characters stay: their choices of this entry keep their step but get a
  null `entryId` (FK SetNull), shown with `entryName: null`. **204** on
  success; deleting again is 404.

### Relationship endpoints

Typed, directed links between a setting's entries
(`SettingRelationshipsController`, routes under
`api/settings/{sid}/relationships`). Any member reads; GameMasters write.
**A Player never sees a link where either end is GM-only.**

| Endpoint | Anonymous | Non-member | Player | GameMaster | Owner |
| --- | --- | --- | --- | --- | --- |
| `GET /api/settings/{sid}/relationships` | 401 | 404 | 200, no GM-only ends | 200 | 200 |
| `POST /api/settings/{sid}/relationships` | 401 | 404 | 403 | **201** | **201** |
| `PUT /api/settings/{sid}/relationships/{id}` | 401 | 404 | 403 | 200 | 200 |
| `DELETE /api/settings/{sid}/relationships/{id}` | 401 | 404 | 403 | **204** | **204** |
| `GET /api/relationship-types` | 401 | 200 | 200 | 200 | 200 |

**Relationship types** ([ADR 0002](../docs/decisions/0002-relationship-type-vocabulary.md)):
a suggested list plus free text, stored as readable labels and compared
**ignoring case** (`RelationshipType` is `citext`). On create and update,
`RelationshipTypes.Normalize` trims the type, collapses inner whitespace and
adopts a suggested type's spelling when it matches ignoring case
(`" located  IN "` is stored as `"Located in"`); custom types keep the GM's
casing. The builder (P7-02) narrows by **any** relationship, in **either**
direction, whatever its type.

- `GET /api/relationship-types` returns `{ suggested: [...] }`, the list to
  offer when linking. The same for every setting; any signed-in user.
- `GET` takes `entryId` (links where that entry is the source **or** the
  target), `type` (normalized like input, then matched ignoring case) and
  `page`/`pageSize`. An `entryId` the caller cannot see (missing, GM-only
  for a Player, or in another setting) is **404** "Entry not found.", the
  same in every case. Sorted by source name, then type, then target name.
- Returns `PagedResult<RelationshipDto>`, each `{ id, source, target,
  relationshipType, description, createdAt, updatedAt }`, where `source`
  and `target` are `{ id, name, entryType }`.
- `POST` body `{ sourceEntryId, targetEntryId, relationshipType,
  description? }`: `relationshipType` 1-60 characters (normalized),
  `description` up to 1000 (blank becomes `null`). The setting is always
  the one in the route; a `campaignSettingId` in the body is ignored.
  Invalid is 400 keyed by the field, including:
  - an entry that is missing or belongs to another setting ("Entry not
    found in this setting.", keyed `SourceEntryId` or `TargetEntryId`);
  - a self-link (keyed `TargetEntryId`).
- A link with the same source, target and type (ignoring case) already
  exists: **409** (also for concurrent creates, through the unique index).
  Another type or the reverse direction is a new link. GM-only entries can
  be linked. Returns **201** `RelationshipDto` with a `Location` header.
- `PUT .../relationships/{id}` body `{ relationshipType, description? }`
  replaces both, with the same rules as `POST` (so omitting `description`
  clears it). **The endpoints cannot change:** a `sourceEntryId` or
  `targetEntryId` that differs from the current one is 400 keyed by that
  field; sending the current value is allowed. To re-link, delete and
  create. Changing to a type the same link already has is 409; changing
  only the case of its own type is fine. Returns 200 `RelationshipDto`.
- `DELETE .../relationships/{id}` removes the link (the entries stay).
  **204**; deleting again is 404.
- A relationship id from another setting is **404** "Relationship not
  found." on `PUT` and `DELETE`, the same as a missing one.

## Character access

`ICharacterAccess` (`Auth/CharacterAccess.cs`) is the one place that decides
what the signed-in user may do with a character. Every character endpoint
calls it **before** touching the character. Each method returns a
`CharacterAccessResult { Character, IsOwner, IsReadOnly }`; the character is
tracked, so an endpoint can change it and call `SaveChangesAsync`.

| Method | Owner, still a member | Owner, removed from the setting | GameMaster or owner of the setting | Other Player, non-member or missing character |
| --- | --- | --- | --- | --- |
| `RequireReadAsync(id)` | read-write | read-only | read-only | 404 |
| `RequireWriteAsync(id)` | read-write | 403 | 403 | 404 |
| `RequireOwnerAsync(id)` (delete) | read-write | read-only | 403 | 404 |

- **Removed players keep their characters, read-only.** Removing a member or
  leaving deletes only the membership row; characters and choices stay as
  they were. Membership is checked on every call, so re-joining through a
  new invite restores write access.
- **Every endpoint that changes a character calls `RequireWriteAsync`**
  (only delete uses `RequireOwnerAsync`). `Characters/CharacterReadOnlyTests`
  finds every non-GET, non-DELETE route under `api/characters/{characterId}`
  and expects the read-only 403 for a removed owner, so a new write endpoint
  (P7 choices, complete, reopen) fails that test until it has a sample body
  in `WriteBodies` there and returns the 403.
  The read-only owner's 403 says "This character is read-only because you
  are no longer a member of its setting."
- A GameMaster reads every character in their setting but never edits or
  deletes someone else's (403 "Only the character's owner can edit it." or
  "... can do this."). A GameMaster's own character (an NPC or their PC) is
  theirs like anyone else's.
- Anyone else gets **404** "Character not found.", the same as a missing
  character, so nobody can probe which ids exist.
- `IsReadOnly` is true whenever the caller cannot edit the character (a
  removed owner or a GameMaster viewer); it is the flag the character DTOs
  return.

### Character endpoints

`CharactersController` (`api/characters`). `CharacterAccessConventionTests`
fails if a controller routed under `api/characters` does not inject
`ICharacterAccess`.

| Endpoint | Anonymous | Non-member, other Player or missing | Owner, member | Owner, removed | GameMaster of the setting |
| --- | --- | --- | --- | --- | --- |
| `GET /api/characters` | 401 | 200, only the caller's own characters | 200 | 200, listed with `isReadOnly: true` | 200, only their own |
| `POST /api/characters` | 401 | 404 (setting) | **201** (any member, GameMasters too) | 404 (setting) | **201**, their own character |
| `GET /api/characters/{id}` | 401 | 404 | 200 | 200, `isReadOnly: true` | 200, `isReadOnly: true` |
| `PUT /api/characters/{id}` | 401 | 404 | 200 | 403 | 403 |
| `DELETE /api/characters/{id}` | 401 | 404 | **204** | **204** | 403 (their own: 204) |

- `GET /api/characters` lists **only the caller's own characters** (the
  Characters page and dashboard), most recently updated first. Query:
  `status` (`Draft`/`Complete`; another value is 400), `settingId`,
  `search` (name contains, ignoring case; wildcards match literally),
  `page`/`pageSize`. Returns `PagedResult<CharacterListItemDto>`: `{ id,
  name, status, settingId, settingName, homelandName, currentStep,
  isReadOnly, updatedAt }`. `homelandName` is the first `homeland` choice's
  entry name or free text (null if none, or if its entry was deleted).
  `isReadOnly` is true once the caller is no longer a member of the setting;
  those characters stay listed.
- `POST /api/characters` body `{ settingId, name? }`. The caller must be a
  member of the setting; a non-member and a missing setting get the same 404
  "Setting not found.". The new character is owned by the caller, `Draft`,
  on `currentStep` 1, with no backstory. `name` is trimmed, at most 100
  characters (400 keyed `Name`); missing or blank becomes "Unnamed
  Character". Returns **201** with `Location: /api/characters/{id}` and a
  `CharacterDetailDto`.
- `CharacterDetailDto`: `{ id, settingId, settingName, ownerUserId,
  ownerDisplayName, name, status, currentStep, backstory, isOwner,
  isReadOnly, choices[], createdAt, updatedAt }`. `isOwner` tells the
  frontend whether to offer delete; `isReadOnly` whether to offer editing.
- Each choice is `{ stepKey, ordinal, entryId, entryName, entryType,
  freeText }`, sorted by `stepKey` then `ordinal`. **A chosen entry that
  later became GM-only keeps showing its name** (decision: never hide or
  break a character after the fact; only the owner and GameMasters can read
  a character). A deleted entry leaves `entryId`, `entryName` and
  `entryType` null.
- `PUT /api/characters/{id}` body `{ name, backstory? }` replaces both.
  Only the owner while still a member (`RequireWriteAsync`); a removed owner
  gets 403 with the read-only reason, a GameMaster 403. `name` is trimmed,
  1-100 characters (blank is 400 keyed `Name`). **`backstory` is plain free
  text** up to 10000 characters (400 keyed `Backstory`), stored exactly as
  sent; the API never renders or sanitizes it as HTML, so the frontend must
  display it as text. Missing or blank clears it. Every successful save
  moves `updatedAt`, even with unchanged values. Returns 200
  `CharacterDetailDto`.
- `DELETE /api/characters/{id}`: only the owner (`RequireOwnerAsync`),
  **also after removal from the setting**, so a read-only character can
  still be cleaned up. A GameMaster gets 403 "Only the character's owner
  can do this." for anyone else's character. The database cascade removes
  its choices; the chosen entries stay. **204**; deleting again is 404.

### GameMaster view of a setting's characters

`GET /api/settings/{sid}/characters` (`SettingCharactersController`): every
character in the setting, **including those of removed players**, most
recently updated first. GameMasters only: Player 403, non-member 404
(identical to a missing setting), anonymous 401. It is read only;
GameMasters have no write endpoint for other people's characters.

- Query: `status`, `search` (character name **or owner display name**
  contains, ignoring case; wildcards match literally), `page`/`pageSize`.
- Returns `PagedResult<SettingCharacterListItemDto>`: `{ id, name, status,
  ownerUserId, ownerDisplayName, ownerIsMember, homelandName, currentStep,
  updatedAt }`. `ownerIsMember` is false for a removed player's character
  (the setting owner always counts as a member). Open one with
  `GET /api/characters/{id}`.

## Builder

The guided character builder's steps are one server-side catalog,
`BuilderSteps` (P7-01), which the frontend wizard and choice validation
both read. The frontend takes step keys, titles and the step count from
`GET /api/builder/steps` and keeps no copy of its own. Adding a step is a
catalog row (and a `CharacterStepKeys` constant), with no migration; never
rename a key, because choice rows store it.

`GET /api/builder/steps` (`BuilderController`): any signed-in user, 401
anonymous. Returns `{ steps: [...] }` in order, each `{ key, order, title,
description, entryType, allowFreeText, maxFreeTextLength, required,
maxSelections }`:

| # | `key` | `entryType` (options) | Free text | `required` | `maxSelections` |
| --- | --- | --- | --- | --- | --- |
| 1 | `setting` | none: chosen at character creation | no | yes | 0 |
| 2 | `homeland` | `Location` | no | yes | 1 |
| 3 | `culture` | `Culture` | no | yes | 1 |
| 4 | `religion` | `Religion` | no | no | 1 |
| 5 | `social_class` | `SocialClass` | no | no | 1 |
| 6 | `profession` | `Profession` | no | yes | 1 |
| 7 | `motivation` | none | yes, `maxFreeTextLength` 2000 | yes | 1 |
| 8 | `review` | none: UI only | no | no | 0 |

- Steps with `maxSelections` 0 (`setting`, `review`) are shown but never
  stored as choices. `setting` is satisfied by the character's `settingId`.
- `maxFreeTextLength` is null unless `allowFreeText`.
- The backstory is not a step; it stays on `PUT /api/characters/{id}`.

## Email

- Confirmation and reset emails go through `IEmailSender<ApplicationUser>`,
  registered in `Email/EmailSetup.cs`.
- **Development** uses `ConsoleEmailSender`: it logs each link (with the user
  id, not the email) so you can click it from the API terminal. Look for
  `[dev email]`.
- **Everywhere else** uses `UnconfiguredEmailSender`, which throws "No email
  provider is configured" rather than silently dropping mail. To send real
  email, implement `IEmailSender<ApplicationUser>` for the chosen provider,
  read its secrets from environment variables, and register it in
  `EmailSetup` in place of `UnconfiguredEmailSender`.
- Links point at frontend pages, built by `FrontendLinks` from
  `App:FrontendBaseUrl` (`http://localhost:3000` in Development; set
  `App__FrontendBaseUrl` elsewhere):
  - `/confirm-email?userId=<id>&code=<code>`
  - `/reset-password?email=<email>&code=<code>`
- `code` is the Identity token Base64Url-encoded (`EmailCodes.Encode`). An
  endpoint that receives it decodes with `EmailCodes.TryDecode`, which returns
  false for a malformed code (answer 400). It is a single-use link parameter,
  not a session token, so the frontend posts it straight back and never stores it.

## Rate limiting

- Policy `auth` (`Security/RateLimitingSetup.cs`): a fixed window of **10
  requests per minute per client IP**, shared by login, register,
  forgot-password, resend-confirmation and account deletion (`DELETE
  /api/users/me`, which checks the password) (`[EnableRateLimiting(RateLimitingSetup.AuthPolicy)]`).
- Policy `invites`: the same limit with its **own counter**, on the
  endpoints that look up an invite code (preview and accept), so
  guessing codes is slow and does not spend the caller's login attempts.
- Other endpoints are not limited.
- Over the limit: **429** problem JSON with a `Retry-After` header and
  `retryAfterSeconds`. Login lockout is separate and returns 423.
- Limits come from `RateLimiting:Auth:PermitLimit` and `WindowSeconds`. The
  test factory raises the limit because every in-memory client shares one
  partition; `RateLimitTests` sets it back to 10 with `WithConfig`.
- Behind a reverse proxy, the client IP comes from `X-Forwarded-For`, trusted
  only from loopback and the IPs in `ForwardedHeaders:KnownProxies` (set
  `ForwardedHeaders__KnownProxies__0`, ...). Without that, every request
  would appear to come from the proxy and share one limit.

## CSRF

Cookie auth sends the cookie on any request to the API, so unsafe requests
must prove they come from our frontend (`Security/CsrfProtectionMiddleware.cs`).
No CSRF token is used, so nothing secret is ever readable by JavaScript.

- Every `POST`, `PUT`, `PATCH` and `DELETE` must send
  **`X-Requested-With: Lorebound`**. A cross-origin page can only add a custom
  header after a CORS preflight, which the origin allowlist refuses, and an
  HTML form cannot add headers at all.
- If the request has an `Origin` header, it must be in `Cors:AllowedOrigins`
  (case and a trailing slash are ignored). This also blocks other origins on
  the same site, which `SameSite=Lax` alone would let through.
- Violations return **403** problem JSON ("Missing or invalid X-Requested-With
  header." or "Origin not allowed."). `GET`, `HEAD` and `OPTIONS` are not checked.
- The check runs right after CORS, before rate limiting and authentication,
  and applies to anonymous endpoints too (login CSRF).
- The frontend API client (P9-03) sends the header on every request; tests get
  it from the factories; `Lorebound.Api.http` includes it on each unsafe request.
