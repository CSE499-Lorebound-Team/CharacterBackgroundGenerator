# Lorebound API reference

<!-- Generated from openapi.json by api.Tests/Conventions/ApiReferenceTests. Do not edit by hand. -->

Every endpoint, generated from the OpenAPI document ([`openapi.json`](openapi.json)). The rules behind it (cookie sign-in, the `X-Requested-With` header, problem+json errors, who may do what) are in the root README under "How the API works" and in [api/README.md](../../api/README.md). To browse and try the endpoints, run the API in Development and open `http://localhost:5110/scalar`.

**Legend:** 🔒 needs the sign-in cookie; ✳ needs the `X-Requested-With: Lorebound` header. Error responses are `application/problem+json`.

## Endpoints

- [Auth](#auth)
- [Builder](#builder)
- [Character builder](#character-builder)
- [Characters](#characters)
- [Dashboard](#dashboard)
- [Health](#health)
- [Invites](#invites)
- [Relationship types](#relationship-types)
- [Setting characters](#setting-characters)
- [Setting entries](#setting-entries)
- [Setting invites](#setting-invites)
- [Setting members](#setting-members)
- [Setting relationships](#setting-relationships)
- [Settings](#settings)
- [Users](#users)

### Auth

| Endpoint | | Request | Responses |
|---|---|---|---|
| `POST /api/auth/register`<br>Creates an account. Does not sign the user in and returns no token. | ✳ | body: [RegisterRequest](#registerrequest) | **201** [RegisteredUserDto](#registereduserdto)<br>**400**<br>**403**<br>**429** |
| `POST /api/auth/login`<br>Signs in and sets the httpOnly auth cookie. The body is only the user summary; wrong password, unknown email and unconfirmed email all get the same 401 so the response does not reveal which accounts exist. | ✳ | body: [LoginRequest](#loginrequest) | **200** [AuthUserDto](#authuserdto)<br>**401**<br>**423**<br>**403**<br>**429** |
| `POST /api/auth/logout`<br>Signs out everywhere: changes the security stamp, so every copy of the user's cookie stops working, and expires this browser's cookie (same name and path as at sign-in). | 🔒✳ |  | **204**<br>**401**<br>**403** |
| `POST /api/auth/confirm-email`<br>Confirms an email with the userId and code from the emailed link. An unknown user, a malformed or wrong code, and an already-confirmed account all get the same 400, so a code cannot be reused. | ✳ | body: [ConfirmEmailRequest](#confirmemailrequest) | **204**<br>**400**<br>**403** |
| `POST /api/auth/resend-confirmation`<br>Sends a fresh confirmation link. Always 204, whether the email is unknown, already confirmed or sent to, so it reveals nothing. | ✳ | body: [ResendConfirmationRequest](#resendconfirmationrequest) | **204**<br>**403**<br>**429** |
| `POST /api/auth/forgot-password`<br>Emails a password reset link. Always 204; the link goes only to an existing account whose email is confirmed, since that proves the inbox belongs to the account. | ✳ | body: [ForgotPasswordRequest](#forgotpasswordrequest) | **204**<br>**403**<br>**429** |
| `POST /api/auth/reset-password`<br>Sets a new password with the email and code from the reset link. Identity changes the security stamp, which ends the user's other sessions and makes the code single-use. | ✳ | body: [ResetPasswordRequest](#resetpasswordrequest) | **204**<br>**400**<br>**403** |

### Builder

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/builder/steps`<br>The builder steps in order, with what each one stores: an entry type from the setting or free text, whether it is required, and how many answers it allows. | 🔒 |  | **200** [BuilderStepsDto](#builderstepsdto)<br>**401** |

### Character builder

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/characters/{characterId}/steps/{stepKey}/options`<br>The options for one step, narrowed by the entries chosen in earlier steps (ADR 0002). Free-text and display-only steps have none. An unknown step is 404. | 🔒 | path `characterId`: string (uuid)<br>path `stepKey`: string | **200** [StepOptionsDto](#stepoptionsdto)<br>**403**<br>**404**<br>**401** |
| `PUT /api/characters/{characterId}/choices/{stepKey}`<br>Replaces one step's answer in a single save and returns the updated character. Entries must be of the step's type, in the character's setting and visible to the caller, unless already chosen for this step; a missing, other-setting or hidden entry gets the same 400. Sending no entries and no text clears the step. Moves `currentStep` past the step if it was behind. A complete character must be reopened first (409). | 🔒✳ | path `characterId`: string (uuid)<br>path `stepKey`: string<br>body: [SaveChoiceRequest](#savechoicerequest) | **200** [CharacterDetailDto](#characterdetaildto)<br>**400**<br>**403**<br>**404**<br>**409**<br>**401** |
| `POST /api/characters/{characterId}/complete`<br>Marks the character Complete once every required step has a valid answer, every chosen entry still exists in the setting with the step's type, no step is stale (P7-04) and the name is not blank. Otherwise 400 with one error per failing step, keyed by step key. Completing a complete character again is a no-op. | 🔒✳ | path `characterId`: string (uuid) | **200** [CharacterDetailDto](#characterdetaildto)<br>**400**<br>**403**<br>**404**<br>**401** |
| `POST /api/characters/{characterId}/reopen`<br>Moves the character back to Draft so its answers can change again. Reopening a draft is a no-op. | 🔒✳ | path `characterId`: string (uuid) | **200** [CharacterDetailDto](#characterdetaildto)<br>**403**<br>**404**<br>**401** |

### Characters

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/characters`<br>The caller's own characters, most recently updated first. Filters: status, settingId, and search (name contains, ignoring case). Characters in a setting the caller has left are included, flagged read-only. | 🔒 | query `status`?: [CharacterStatus](#characterstatus)<br>query `settingId`?: string (uuid)<br>query `search`?: string<br>query `page`?: integer (int32)<br>query `pageSize`?: integer (int32) | **200** [PagedResultOfCharacterListItemDto](#pagedresultofcharacterlistitemdto)<br>**400**<br>**401** |
| `POST /api/characters`<br>Starts a draft character owned by the caller, on step 1. Any member of the setting may create one, GameMasters included (NPCs or their own PC); a non-member or missing setting gets 404. | 🔒✳ | body: [CreateCharacterRequest](#createcharacterrequest) | **201** [CharacterDetailDto](#characterdetaildto)<br>**400**<br>**404**<br>**401**<br>**403** |
| `GET /api/characters/{characterId}`<br>One character with its choices. The owner (even read-only) and the setting's GameMasters may read it; anyone else gets 404. | 🔒 | path `characterId`: string (uuid) | **200** [CharacterDetailDto](#characterdetaildto)<br>**404**<br>**401** |
| `PUT /api/characters/{characterId}`<br>Replaces the name and backstory. Only the owner while still a member of the setting; a removed owner or a GameMaster gets 403. The backstory is free text, never rendered as HTML by the API. | 🔒✳ | path `characterId`: string (uuid)<br>body: [UpdateCharacterRequest](#updatecharacterrequest) | **200** [CharacterDetailDto](#characterdetaildto)<br>**400**<br>**403**<br>**404**<br>**401** |
| `DELETE /api/characters/{characterId}`<br>Deletes the character and, through the database cascade, its choices. Only the owner, also after removal from the setting (a read-only character can still be deleted); a GameMaster gets 403. | 🔒✳ | path `characterId`: string (uuid) | **204**<br>**403**<br>**404**<br>**401** |

### Dashboard

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/dashboard`<br>Counts, the 5 most recently updated settings the caller belongs to and characters they own, and the 10 most recent changes across those settings, their visible entries and the caller's characters. Players never see activity on GM-only entries. | 🔒 |  | **200** [DashboardDto](#dashboarddto)<br>**401** |

### Health

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/Health`<br>Whether the API is up and can reach its database. Public, for load balancers and uptime checks: 200 with `database: "connected"`, or 503 with `database: "unreachable"`. |  |  | **200** [HealthResponse](#healthresponse)<br>**503** |

### Invites

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/invites/{code}`<br>What the user would join. Reading a preview never uses up the invite. | 🔒 | path `code`: string | **200** [InvitePreviewDto](#invitepreviewdto)<br>**404**<br>**429**<br>**401** |
| `POST /api/invites/{code}/accept`<br>Joins the setting as a Player and uses up one use of the invite. A user who already belongs gets 200 with their current role and uses nothing, so retrying (a double click, a reload) is safe. Membership is checked before validity, so the retry of an accept that took the last use is still 200; a member learns nothing new from it. Everyone else gets the identical 404 for an unusable code. | 🔒✳ | path `code`: string | **200** [AcceptInviteResponse](#acceptinviteresponse)<br>**404**<br>**429**<br>**401**<br>**403** |

### Relationship types

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/relationship-types`<br>The suggested relationship types, in display order. Any other label is allowed too; these keep wording consistent. | 🔒 |  | **200** [RelationshipTypesDto](#relationshiptypesdto)<br>**401** |

### Setting characters

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/settings/{settingId}/characters`<br>Every character in the setting, removed players' included, most recently updated first. GameMasters only (Player 403, non-member 404). Filters: status, and search (character or owner name contains, ignoring case). | 🔒 | path `settingId`: string (uuid)<br>query `status`?: [CharacterStatus](#characterstatus)<br>query `search`?: string<br>query `page`?: integer (int32)<br>query `pageSize`?: integer (int32) | **200** [PagedResultOfSettingCharacterListItemDto](#pagedresultofsettingcharacterlistitemdto)<br>**400**<br>**403**<br>**404**<br>**401** |

### Setting entries

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/settings/{settingId}/entries`<br>Entries of the setting by name, filtered by type, search (name or description, case-insensitive) and gmOnly. For a Player, `gmOnly=true` matches nothing, since they never see GM-only entries. | 🔒 | path `settingId`: string (uuid)<br>query `type`?: [SettingEntryType](#settingentrytype)<br>query `search`?: string<br>query `gmOnly`?: boolean<br>query `page`?: integer (int32)<br>query `pageSize`?: integer (int32) | **200** [PagedResultOfEntryListItemDto](#pagedresultofentrylistitemdto)<br>**400**<br>**404**<br>**401** |
| `POST /api/settings/{settingId}/entries`<br>Adds an entry. GameMasters only. The name is trimmed and must be unique per type within the setting, ignoring case (409 otherwise). | 🔒✳ | path `settingId`: string (uuid)<br>body: [CreateEntryRequest](#createentryrequest) | **201** [SettingEntryDto](#settingentrydto)<br>**400**<br>**403**<br>**404**<br>**409**<br>**401** |
| `GET /api/settings/{settingId}/entries/{entryId}`<br>One entry with its outgoing and incoming relationships, each sorted by the other entry's name. A Player asking for a GM-only entry gets the same 404 as for a missing one, and relationships to GM-only entries are left out, so a Player cannot learn a hidden entry exists. | 🔒 | path `settingId`: string (uuid)<br>path `entryId`: string (uuid) | **200** [EntryDetailDto](#entrydetaildto)<br>**404**<br>**401** |
| `PUT /api/settings/{settingId}/entries/{entryId}`<br>Replaces an entry's name, type, description and secrecy. GameMasters only; same rules as create, and the name check ignores the entry itself. Making an entry GM-only does not change characters that already chose it (decision recorded in P6-05). | 🔒✳ | path `settingId`: string (uuid)<br>path `entryId`: string (uuid)<br>body: [UpdateEntryRequest](#updateentryrequest) | **200** [SettingEntryDto](#settingentrydto)<br>**400**<br>**403**<br>**404**<br>**409**<br>**401** |
| `DELETE /api/settings/{settingId}/entries/{entryId}`<br>Deletes an entry. GameMasters only. An entry that still has relationships (either direction) or that characters have chosen is 409 with `relationshipCount` and `characterCount` and nothing changes, unless force is true: then its relationships and the entry go together in one transaction, and the characters keep their choices without the entry (P6-09). | 🔒✳ | path `settingId`: string (uuid)<br>path `entryId`: string (uuid)<br>query `force`?: boolean | **204**<br>**403**<br>**404**<br>**409**<br>**401** |

### Setting invites

| Endpoint | | Request | Responses |
|---|---|---|---|
| `POST /api/settings/{settingId}/invites`<br>Creates a Player invite. The body is optional; expiry defaults to 7 days and uses to unlimited. | 🔒✳ | path `settingId`: string (uuid)<br>body: any | **201** [InviteDto](#invitedto)<br>**400**<br>**403**<br>**404**<br>**409**<br>**401** |
| `GET /api/settings/{settingId}/invites`<br>Every invite of the setting, newest first, with its current status. | 🔒 | path `settingId`: string (uuid)<br>query `page`?: integer (int32)<br>query `pageSize`?: integer (int32) | **200** [PagedResultOfInviteDto](#pagedresultofinvitedto)<br>**400**<br>**403**<br>**404**<br>**401** |
| `DELETE /api/settings/{settingId}/invites/{inviteId}`<br>Revokes an invite so it can no longer be accepted. The row is kept; revoking twice is a no-op 204. | 🔒✳ | path `settingId`: string (uuid)<br>path `inviteId`: string (uuid) | **204**<br>**403**<br>**404**<br>**401** |

### Setting members

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/settings/{settingId}/members`<br>Owner first, then GameMasters, then Players, each oldest first. | 🔒 | path `settingId`: string (uuid)<br>query `page`?: integer (int32)<br>query `pageSize`?: integer (int32) | **200** [PagedResultOfMemberDto](#pagedresultofmemberdto)<br>**400**<br>**404**<br>**401** |
| `PATCH /api/settings/{settingId}/members/{userId}`<br>Promotes or demotes a member. Owner only. Setting the role a member already has is a no-op 200; the owner is always a GameMaster. | 🔒✳ | path `settingId`: string (uuid)<br>path `userId`: string (uuid)<br>body: [UpdateMemberRoleRequest](#updatememberrolerequest) | **200** [MemberDto](#memberdto)<br>**400**<br>**403**<br>**404**<br>**409**<br>**401** |
| `DELETE /api/settings/{settingId}/members/{userId}`<br>Removes a member, or lets a member leave (their own userId). Any member but the owner may leave; GameMasters remove Players; only the owner removes a GameMaster; nobody removes the owner. Takes effect at once: the next request from the removed user gets 404 on the setting. Their characters are kept, read-only (P6-10, see ICharacterAccess). | 🔒✳ | path `settingId`: string (uuid)<br>path `userId`: string (uuid) | **204**<br>**403**<br>**404**<br>**409**<br>**401** |

### Setting relationships

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/settings/{settingId}/relationships`<br>The setting's relationships, sorted by source name, type and target name. entryId keeps the links of one entry in either direction (404 if the caller cannot see that entry); type keeps one relationship type, ignoring case. | 🔒 | path `settingId`: string (uuid)<br>query `entryId`?: string (uuid)<br>query `type`?: string<br>query `page`?: integer (int32)<br>query `pageSize`?: integer (int32) | **200** [PagedResultOfRelationshipDto](#pagedresultofrelationshipdto)<br>**400**<br>**404**<br>**401** |
| `POST /api/settings/{settingId}/relationships`<br>Links two entries. GameMasters only. Both entries must belong to the setting in the route (400 otherwise, the same for a missing entry); a self-link is 400 and an existing (source, target, type) link is 409. | 🔒✳ | path `settingId`: string (uuid)<br>body: [CreateRelationshipRequest](#createrelationshiprequest) | **201** [RelationshipDto](#relationshipdto)<br>**400**<br>**403**<br>**404**<br>**409**<br>**401** |
| `PUT /api/settings/{settingId}/relationships/{relationshipId}`<br>Replaces a relationship's type and description. GameMasters only. The endpoints are immutable: a `sourceEntryId` or `targetEntryId` that differs from the current one is 400 (delete and recreate to re-link). Changing to a type the same link already has is 409. | 🔒✳ | path `settingId`: string (uuid)<br>path `relationshipId`: string (uuid)<br>body: [UpdateRelationshipRequest](#updaterelationshiprequest) | **200** [RelationshipDto](#relationshipdto)<br>**400**<br>**403**<br>**404**<br>**409**<br>**401** |
| `DELETE /api/settings/{settingId}/relationships/{relationshipId}`<br>Deletes a relationship. GameMasters only; the entries stay. | 🔒✳ | path `settingId`: string (uuid)<br>path `relationshipId`: string (uuid) | **204**<br>**403**<br>**404**<br>**401** |

### Settings

| Endpoint | | Request | Responses |
|---|---|---|---|
| `POST /api/settings`<br>Creates a setting owned by the caller, who becomes its GameMaster. The name is trimmed and must be unique among the caller's own settings, ignoring case (409 otherwise). | 🔒✳ | body: [CreateSettingRequest](#createsettingrequest) | **201** [SettingDetailDto](#settingdetaildto)<br>**400**<br>**409**<br>**401**<br>**403** |
| `GET /api/settings`<br>The settings the caller owns or belongs to, most recently updated first. search matches the name (contains, ignoring case); role is `gm` or `player` (400 otherwise). Players' entry counts leave out GM-only entries. | 🔒 | query `search`?: string<br>query `role`?: string<br>query `page`?: integer (int32)<br>query `pageSize`?: integer (int32) | **200** [PagedResultOfSettingListItemDto](#pagedresultofsettinglistitemdto)<br>**400**<br>**401** |
| `GET /api/settings/{id}`<br>One setting with the caller's role, the member count and entry counts by type (GM-only entries left out for Players). Any member may read it; anyone else gets 404. | 🔒 | path `id`: string (uuid) | **200** [SettingDetailDto](#settingdetaildto)<br>**404**<br>**401** |
| `PUT /api/settings/{id}`<br>Renames the setting and replaces its description. GameMasters only (Players get 403); the name must stay unique among the owner's settings (409). | 🔒✳ | path `id`: string (uuid)<br>body: [UpdateSettingRequest](#updatesettingrequest) | **200** [SettingDetailDto](#settingdetaildto)<br>**400**<br>**403**<br>**404**<br>**409**<br>**401** |
| `DELETE /api/settings/{id}`<br>Deletes the setting with its entries, relationships, memberships, invites and characters. The owner only; other members get 403. | 🔒✳ | path `id`: string (uuid) | **204**<br>**403**<br>**404**<br>**401** |

### Users

| Endpoint | | Request | Responses |
|---|---|---|---|
| `GET /api/users/me`<br>The signed-in user's profile. | 🔒 |  | **200** [UserProfileDto](#userprofiledto)<br>**401** |
| `PUT /api/users/me`<br>Changes the display name (1-60 characters, trimmed). Email and password changes are out of scope for now. | 🔒✳ | body: [UpdateProfileRequest](#updateprofilerequest) | **200** [UserProfileDto](#userprofiledto)<br>**400**<br>**401**<br>**403** |
| `DELETE /api/users/me`<br>Deletes the account after re-checking the password (400 keyed `Password` when wrong; rate limited like login). Blocked with 409 and the list of settings while the user owns a setting that has other members: there is no ownership transfer, so they must delete the setting or remove its members first. Otherwise, in one transaction: the settings they own (with everything in them), their memberships, characters and invites elsewhere, then the user. Signs out; 204. | 🔒✳ | body: [DeleteAccountRequest](#deleteaccountrequest) | **204**<br>**400**<br>**401**<br>**409**<br>**429**<br>**403** |

## Schemas

### AcceptInviteResponse

| Field | Type | Required |
|---|---|---|
| `settingId` | string (uuid) | yes |
| `myRole` | [SettingRole](#settingrole) | yes |

### AuthUserDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `email` | string | yes |
| `displayName` | string | yes |

### BuilderStepDto

| Field | Type | Required |
|---|---|---|
| `key` | string | yes |
| `order` | integer (int32) | yes |
| `title` | string | yes |
| `description` | string | yes |
| `entryType` | any | yes |
| `allowFreeText` | boolean | yes |
| `maxFreeTextLength` | integer (int32) or null | yes |
| `required` | boolean | yes |
| `maxSelections` | integer (int32) | yes |

### BuilderStepsDto

| Field | Type | Required |
|---|---|---|
| `steps` | [BuilderStepDto](#builderstepdto)[] | yes |

### CharacterChoiceDto

| Field | Type | Required |
|---|---|---|
| `stepKey` | string | yes |
| `ordinal` | integer (int32) | yes |
| `entryId` | string (uuid) or null | yes |
| `entryName` | string or null | yes |
| `entryType` | any | yes |
| `freeText` | string or null | yes |

### CharacterDetailDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `settingId` | string (uuid) | yes |
| `settingName` | string | yes |
| `ownerUserId` | string (uuid) | yes |
| `ownerDisplayName` | string | yes |
| `name` | string | yes |
| `status` | [CharacterStatus](#characterstatus) | yes |
| `currentStep` | integer (int32) | yes |
| `backstory` | string or null | yes |
| `isOwner` | boolean | yes |
| `isReadOnly` | boolean | yes |
| `choices` | [CharacterChoiceDto](#characterchoicedto)[] | yes |
| `createdAt` | string (date-time) | yes |
| `updatedAt` | string (date-time) | yes |
| `staleSteps` | string[] | yes |

### CharacterListItemDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `name` | string | yes |
| `status` | [CharacterStatus](#characterstatus) | yes |
| `settingId` | string (uuid) | yes |
| `settingName` | string | yes |
| `homelandName` | string or null | yes |
| `currentStep` | integer (int32) | yes |
| `isReadOnly` | boolean | yes |
| `updatedAt` | string (date-time) | yes |

### CharacterStatus

One of: `Draft`, `Complete`

### ConfirmEmailRequest

| Field | Type | Required |
|---|---|---|
| `userId` | string (uuid) or null | yes |
| `code` | string or null | yes |

### CreateCharacterRequest

| Field | Type | Required |
|---|---|---|
| `settingId` | string (uuid) or null | yes |
| `name` | string or null | yes |

### CreateEntryRequest

| Field | Type | Required |
|---|---|---|
| `name` | string | yes |
| `entryType` | any | yes |
| `description` | string or null | yes |
| `isGmOnly` | boolean |  |

### CreateInviteRequest

| Field | Type | Required |
|---|---|---|
| `expiresInDays` | integer (int32) or null | yes |
| `maxUses` | integer (int32) or null | yes |

### CreateRelationshipRequest

| Field | Type | Required |
|---|---|---|
| `sourceEntryId` | string (uuid) or null | yes |
| `targetEntryId` | string (uuid) or null | yes |
| `relationshipType` | string | yes |
| `description` | string or null | yes |

### CreateSettingRequest

| Field | Type | Required |
|---|---|---|
| `name` | string | yes |
| `description` | string or null | yes |

### DashboardActivityDto

| Field | Type | Required |
|---|---|---|
| `kind` | [DashboardActivityKind](#dashboardactivitykind) | yes |
| `text` | string | yes |
| `at` | string (date-time) | yes |
| `id` | string (uuid) | yes |
| `settingId` | string (uuid) | yes |

### DashboardActivityKind

One of: `Setting`, `Entry`, `Character`

### DashboardCountsDto

| Field | Type | Required |
|---|---|---|
| `settingsAsGm` | integer (int32) | yes |
| `settingsAsPlayer` | integer (int32) | yes |
| `charactersDraft` | integer (int32) | yes |
| `charactersComplete` | integer (int32) | yes |

### DashboardDto

| Field | Type | Required |
|---|---|---|
| `counts` | [DashboardCountsDto](#dashboardcountsdto) | yes |
| `recentSettings` | [SettingListItemDto](#settinglistitemdto)[] | yes |
| `recentCharacters` | [CharacterListItemDto](#characterlistitemdto)[] | yes |
| `recentActivity` | [DashboardActivityDto](#dashboardactivitydto)[] | yes |

### DeleteAccountRequest

| Field | Type | Required |
|---|---|---|
| `password` | string or null | yes |

### EntryDetailDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `campaignSettingId` | string (uuid) | yes |
| `name` | string | yes |
| `entryType` | [SettingEntryType](#settingentrytype) | yes |
| `description` | string or null | yes |
| `isGmOnly` | boolean or null | yes |
| `outgoing` | [EntryRelationshipDto](#entryrelationshipdto)[] | yes |
| `incoming` | [EntryRelationshipDto](#entryrelationshipdto)[] | yes |
| `createdAt` | string (date-time) | yes |
| `updatedAt` | string (date-time) | yes |

### EntryListItemDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `name` | string | yes |
| `entryType` | [SettingEntryType](#settingentrytype) | yes |
| `description` | string or null | yes |
| `isGmOnly` | boolean or null | yes |
| `relationshipCount` | integer (int32) | yes |
| `updatedAt` | string (date-time) | yes |

### EntryRelationshipDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `otherEntryId` | string (uuid) | yes |
| `otherEntryName` | string | yes |
| `otherEntryType` | [SettingEntryType](#settingentrytype) | yes |
| `relationshipType` | string | yes |
| `description` | string or null | yes |

### ForgotPasswordRequest

| Field | Type | Required |
|---|---|---|
| `email` | string or null | yes |

### HealthResponse

| Field | Type | Required |
|---|---|---|
| `status` | string | yes |
| `application` | string | yes |
| `database` | string | yes |

### InviteDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `code` | string | yes |
| `joinUrl` | string | yes |
| `status` | [InviteStatus](#invitestatus) | yes |
| `expiresAt` | string (date-time) or null | yes |
| `maxUses` | integer (int32) or null | yes |
| `useCount` | integer (int32) | yes |
| `revokedAt` | string (date-time) or null | yes |
| `createdAt` | string (date-time) | yes |

### InvitePreviewDto

| Field | Type | Required |
|---|---|---|
| `settingName` | string | yes |
| `gmDisplayName` | string | yes |
| `alreadyMember` | boolean | yes |

### InviteStatus

One of: `Active`, `Expired`, `Revoked`, `Exhausted`

### LoginRequest

| Field | Type | Required |
|---|---|---|
| `email` | string or null | yes |
| `password` | string or null | yes |
| `rememberMe` | boolean |  |

### MemberDto

| Field | Type | Required |
|---|---|---|
| `userId` | string (uuid) | yes |
| `displayName` | string | yes |
| `role` | [SettingRole](#settingrole) | yes |
| `isOwner` | boolean | yes |
| `joinedAt` | string (date-time) | yes |

### PagedResultOfCharacterListItemDto

| Field | Type | Required |
|---|---|---|
| `items` | [CharacterListItemDto](#characterlistitemdto)[] | yes |
| `page` | integer (int32) | yes |
| `pageSize` | integer (int32) | yes |
| `totalCount` | integer (int32) | yes |

### PagedResultOfEntryListItemDto

| Field | Type | Required |
|---|---|---|
| `items` | [EntryListItemDto](#entrylistitemdto)[] | yes |
| `page` | integer (int32) | yes |
| `pageSize` | integer (int32) | yes |
| `totalCount` | integer (int32) | yes |

### PagedResultOfInviteDto

| Field | Type | Required |
|---|---|---|
| `items` | [InviteDto](#invitedto)[] | yes |
| `page` | integer (int32) | yes |
| `pageSize` | integer (int32) | yes |
| `totalCount` | integer (int32) | yes |

### PagedResultOfMemberDto

| Field | Type | Required |
|---|---|---|
| `items` | [MemberDto](#memberdto)[] | yes |
| `page` | integer (int32) | yes |
| `pageSize` | integer (int32) | yes |
| `totalCount` | integer (int32) | yes |

### PagedResultOfRelationshipDto

| Field | Type | Required |
|---|---|---|
| `items` | [RelationshipDto](#relationshipdto)[] | yes |
| `page` | integer (int32) | yes |
| `pageSize` | integer (int32) | yes |
| `totalCount` | integer (int32) | yes |

### PagedResultOfSettingCharacterListItemDto

| Field | Type | Required |
|---|---|---|
| `items` | [SettingCharacterListItemDto](#settingcharacterlistitemdto)[] | yes |
| `page` | integer (int32) | yes |
| `pageSize` | integer (int32) | yes |
| `totalCount` | integer (int32) | yes |

### PagedResultOfSettingListItemDto

| Field | Type | Required |
|---|---|---|
| `items` | [SettingListItemDto](#settinglistitemdto)[] | yes |
| `page` | integer (int32) | yes |
| `pageSize` | integer (int32) | yes |
| `totalCount` | integer (int32) | yes |

### ProblemDetails

| Field | Type | Required |
|---|---|---|
| `type` | string or null |  |
| `title` | string or null |  |
| `status` | integer (int32) or null |  |
| `detail` | string or null |  |
| `instance` | string or null |  |

### RegisterRequest

| Field | Type | Required |
|---|---|---|
| `email` | string or null | yes |
| `password` | string or null | yes |
| `displayName` | string or null | yes |

### RegisteredUserDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `email` | string | yes |
| `displayName` | string | yes |
| `emailConfirmed` | boolean | yes |

### RelationshipDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `source` | [RelationshipEndpointDto](#relationshipendpointdto) | yes |
| `target` | [RelationshipEndpointDto](#relationshipendpointdto) | yes |
| `relationshipType` | string | yes |
| `description` | string or null | yes |
| `createdAt` | string (date-time) | yes |
| `updatedAt` | string (date-time) | yes |

### RelationshipEndpointDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `name` | string | yes |
| `entryType` | [SettingEntryType](#settingentrytype) | yes |

### RelationshipTypesDto

| Field | Type | Required |
|---|---|---|
| `suggested` | string[] | yes |

### ResendConfirmationRequest

| Field | Type | Required |
|---|---|---|
| `email` | string or null | yes |

### ResetPasswordRequest

| Field | Type | Required |
|---|---|---|
| `email` | string or null | yes |
| `code` | string or null | yes |
| `newPassword` | string or null | yes |

### SaveChoiceRequest

| Field | Type | Required |
|---|---|---|
| `entryIds` | string (uuid)[] or null | yes |
| `freeText` | string or null | yes |

### SettingCharacterListItemDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `name` | string | yes |
| `status` | [CharacterStatus](#characterstatus) | yes |
| `ownerUserId` | string (uuid) | yes |
| `ownerDisplayName` | string | yes |
| `ownerIsMember` | boolean | yes |
| `homelandName` | string or null | yes |
| `currentStep` | integer (int32) | yes |
| `updatedAt` | string (date-time) | yes |

### SettingDetailDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `name` | string | yes |
| `description` | string or null | yes |
| `myRole` | string | yes |
| `isOwner` | boolean | yes |
| `ownerDisplayName` | string | yes |
| `memberCount` | integer (int32) | yes |
| `entryCountsByType` | map of integer (int32) | yes |
| `createdAt` | string (date-time) | yes |
| `updatedAt` | string (date-time) | yes |

### SettingEntryDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `campaignSettingId` | string (uuid) | yes |
| `name` | string | yes |
| `description` | string or null | yes |
| `entryType` | [SettingEntryType](#settingentrytype) | yes |
| `isGmOnly` | boolean or null | yes |
| `createdAt` | string (date-time) | yes |
| `updatedAt` | string (date-time) | yes |

### SettingEntryType

One of: `Location`, `Culture`, `Religion`, `Faction`, `Organization`, `Profession`, `SocialClass`, `HistoricalEvent`, `Person`, `Other`, ``

### SettingListItemDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `name` | string | yes |
| `description` | string or null | yes |
| `entryCount` | integer (int32) | yes |
| `myRole` | string | yes |
| `isOwner` | boolean | yes |
| `ownerDisplayName` | string | yes |
| `updatedAt` | string (date-time) | yes |

### SettingRole

One of: `GameMaster`, `Player`

### StepOptionDto

| Field | Type | Required |
|---|---|---|
| `entryId` | string (uuid) | yes |
| `name` | string | yes |
| `description` | string or null | yes |

### StepOptionsDto

| Field | Type | Required |
|---|---|---|
| `stepKey` | string | yes |
| `narrowed` | boolean | yes |
| `options` | [StepOptionDto](#stepoptiondto)[] | yes |

### UpdateCharacterRequest

| Field | Type | Required |
|---|---|---|
| `name` | string | yes |
| `backstory` | string or null | yes |

### UpdateEntryRequest

| Field | Type | Required |
|---|---|---|
| `name` | string | yes |
| `entryType` | any | yes |
| `description` | string or null | yes |
| `isGmOnly` | boolean |  |

### UpdateMemberRoleRequest

| Field | Type | Required |
|---|---|---|
| `role` | any | yes |

### UpdateProfileRequest

| Field | Type | Required |
|---|---|---|
| `displayName` | string or null | yes |

### UpdateRelationshipRequest

| Field | Type | Required |
|---|---|---|
| `relationshipType` | string | yes |
| `description` | string or null | yes |
| `sourceEntryId` | string (uuid) or null |  |
| `targetEntryId` | string (uuid) or null |  |

### UpdateSettingRequest

| Field | Type | Required |
|---|---|---|
| `name` | string | yes |
| `description` | string or null | yes |

### UserProfileDto

| Field | Type | Required |
|---|---|---|
| `id` | string (uuid) | yes |
| `email` | string | yes |
| `displayName` | string | yes |
| `emailConfirmed` | boolean | yes |
| `createdAt` | string (date-time) | yes |

### ValidatedRequest

| Field | Type | Required |
|---|---|---|
| `name` | string or null | yes |

