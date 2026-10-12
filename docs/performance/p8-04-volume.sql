-- P8-04: volume data for the query review. Run ONLY against a throwaway
-- database (see docs/performance/p8-04-query-review.md), never against a
-- database you care about: it inserts ~60k rows with fake users.
--
-- Fixed ids, used by p8-04-explain.sql:
--   GameMaster (owns Osepia and 24 more settings) aaaaaaaa-0000-0000-0000-000000000001
--   Player (member of Osepia and 25 other settings) aaaaaaaa-0000-0000-0000-000000000002
--   Osepia                                         bbbbbbbb-0000-0000-0000-000000000001

BEGIN;

-- Users: the GameMaster, the Player and 300 others.
INSERT INTO "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
    "DisplayName", "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled",
    "LockoutEnabled", "AccessFailedCount", "CreatedAt")
SELECT id, email, upper(email), email, upper(email), name, true, false, false, true, 0, now()
FROM (
  SELECT 'aaaaaaaa-0000-0000-0000-000000000001'::uuid AS id, 'gm@perf.local' AS email, 'Perf GM' AS name
  UNION ALL
  SELECT 'aaaaaaaa-0000-0000-0000-000000000002'::uuid, 'player@perf.local', 'Perf Player'
  UNION ALL
  SELECT gen_random_uuid(), 'user' || i || '@perf.local', 'User ' || i FROM generate_series(1, 300) i
) u;

-- Settings: Osepia and 24 more owned by the GameMaster; 25 owned by other
-- users, each with the Player as a member.
INSERT INTO "CampaignSettings" ("Id", "Name", "Description", "OwnerUserId", "CreatedAt", "UpdatedAt")
SELECT 'bbbbbbbb-0000-0000-0000-000000000001', 'Osepia', 'The big one',
       'aaaaaaaa-0000-0000-0000-000000000001', now() - interval '400 days', now() - interval '1 day';
INSERT INTO "CampaignSettings" ("Id", "Name", "Description", "OwnerUserId", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), 'GM setting ' || i, null, 'aaaaaaaa-0000-0000-0000-000000000001',
       now() - interval '400 days', now() - (random() * interval '365 days')
FROM generate_series(1, 24) i;
INSERT INTO "CampaignSettings" ("Id", "Name", "Description", "OwnerUserId", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), 'Shared setting ' || i, null,
       (SELECT "Id" FROM "AspNetUsers" WHERE "Email" = 'user' || i || '@perf.local'),
       now() - interval '400 days', now() - (random() * interval '365 days')
FROM generate_series(1, 25) i;
-- Other tenants: 500 settings the GameMaster and the Player cannot see, so
-- the review shows whether a query's cost follows the caller's data or the
-- whole database.
INSERT INTO "CampaignSettings" ("Id", "Name", "Description", "OwnerUserId", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), 'Other tenant ' || i, null,
       (SELECT "Id" FROM "AspNetUsers" WHERE "Email" = 'user' || (26 + i % 275) || '@perf.local'),
       now() - interval '400 days', now() - (random() * interval '365 days')
FROM generate_series(1, 500) i;

-- Memberships: every owner as GameMaster; the Player in Osepia and the 25
-- shared settings; 300 other users as Players in Osepia.
INSERT INTO "SettingMemberships" ("Id", "CampaignSettingId", "UserId", "Role", "JoinedAt", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), "Id", "OwnerUserId", 'GameMaster', now(), now(), now() FROM "CampaignSettings";
INSERT INTO "SettingMemberships" ("Id", "CampaignSettingId", "UserId", "Role", "JoinedAt", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), "Id", 'aaaaaaaa-0000-0000-0000-000000000002', 'Player', now(), now(), now()
FROM "CampaignSettings" WHERE "Name" = 'Osepia' OR "Name" LIKE 'Shared setting %';
INSERT INTO "SettingMemberships" ("Id", "CampaignSettingId", "UserId", "Role", "JoinedAt", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), 'bbbbbbbb-0000-0000-0000-000000000001', "Id", 'Player', now(), now(), now()
FROM "AspNetUsers" WHERE "Email" LIKE 'user%@perf.local';

-- Entries: 10,000 in Osepia, 400 in each other visible setting and 100 in
-- each other tenant's (~80k in all), types in
-- rotation, every tenth (the Factions) GM-only, ~2% (visible ones)
-- mentioning "harbor".
INSERT INTO "SettingEntries" ("Id", "CampaignSettingId", "Name", "Description", "EntryType", "IsGmOnly", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), s."Id", 'Entry ' || i,
       CASE WHEN i % 50 = 5 THEN 'A busy harbor town. ' ELSE '' END || 'Lore ' || md5(i::text),
       (ARRAY['Location','Culture','Religion','Faction','Organization','Profession','SocialClass','HistoricalEvent','Person','Other'])[1 + i % 10],
       i % 10 = 3, now() - interval '400 days', now() - (random() * interval '365 days')
FROM "CampaignSettings" s
CROSS JOIN LATERAL generate_series(1, CASE WHEN s."Name" = 'Osepia' THEN 10000
                                           WHEN s."Name" LIKE 'Other tenant %' THEN 100
                                           ELSE 400 END) i;

-- Relationships: 10,000 in Osepia.
INSERT INTO "SettingEntryRelationships" ("Id", "CampaignSettingId", "SourceEntryId", "TargetEntryId", "RelationshipType", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), 'bbbbbbbb-0000-0000-0000-000000000001', src."Id", tgt."Id", 'Located in', now(), now()
FROM generate_series(1, 10000) i
JOIN "SettingEntries" src ON src."CampaignSettingId" = 'bbbbbbbb-0000-0000-0000-000000000001' AND src."Name" = 'Entry ' || i
JOIN "SettingEntries" tgt ON tgt."CampaignSettingId" = 'bbbbbbbb-0000-0000-0000-000000000001'
    AND tgt."Name" = 'Entry ' || (1 + (i * 7) % 10000)
WHERE src."Id" <> tgt."Id";

-- Characters: 2,000 for the Player across their settings, 5 for each other
-- Osepia player (1,500), each with a homeland.
INSERT INTO "Characters" ("Id", "CampaignSettingId", "OwnerUserId", "Name", "Status", "CurrentStep", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), m."CampaignSettingId", m."UserId", 'Hero ' || i,
       CASE WHEN i % 3 = 0 THEN 'Complete' ELSE 'Draft' END, 3,
       now() - interval '400 days', now() - (random() * interval '365 days')
FROM "SettingMemberships" m
CROSS JOIN LATERAL generate_series(1, CASE WHEN m."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002' THEN 77 ELSE 5 END) i
WHERE m."Role" = 'Player';
INSERT INTO "CharacterChoices" ("Id", "CharacterId", "StepKey", "Ordinal", "EntryId")
SELECT gen_random_uuid(), c."Id", 'homeland', 0,
       (SELECT e."Id" FROM "SettingEntries" e
        WHERE e."CampaignSettingId" = c."CampaignSettingId" AND e."EntryType" = 'Location' LIMIT 1)
FROM "Characters" c;

COMMIT;

ANALYZE;

SELECT (SELECT count(*) FROM "AspNetUsers") AS users,
       (SELECT count(*) FROM "CampaignSettings") AS settings,
       (SELECT count(*) FROM "SettingEntries") AS entries,
       (SELECT count(*) FROM "SettingEntryRelationships") AS relationships,
       (SELECT count(*) FROM "SettingMemberships") AS memberships,
       (SELECT count(*) FROM "Characters") AS characters,
       (SELECT count(*) FROM "CharacterChoices") AS choices;
