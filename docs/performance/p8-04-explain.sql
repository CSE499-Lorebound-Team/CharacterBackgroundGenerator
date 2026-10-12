-- P8-04: EXPLAIN ANALYZE of the SQL EF Core generates for entry search, the
-- entry list and the dashboard, copied from the Development SQL log with the
-- parameters filled in. Run after p8-04-volume.sql on the same throwaway
-- database; queries run as the Player (aaaaaaaa-...-0002), who also filters
-- out GM-only entries.
\timing off
\pset pager off

\echo '== Q1 entry search: count (search=harbor) =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT count(*)::int
FROM "SettingEntries" AS s
WHERE s."CampaignSettingId" = 'bbbbbbbb-0000-0000-0000-000000000001' AND NOT (s."IsGmOnly")
  AND (s."Name" ILIKE '%harbor%' ESCAPE '\' OR (s."Description" IS NOT NULL AND s."Description" ILIKE '%harbor%' ESCAPE '\'));

\echo '== Q2 entry search: first page with relationship counts =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT s."Id", s."Name", s."EntryType", s."Description", (
    SELECT count(*)::int FROM "SettingEntryRelationships" AS s0
    INNER JOIN "SettingEntries" AS s1 ON s0."TargetEntryId" = s1."Id"
    WHERE s."Id" = s0."SourceEntryId" AND NOT (s1."IsGmOnly")) + (
    SELECT count(*)::int FROM "SettingEntryRelationships" AS s2
    INNER JOIN "SettingEntries" AS s3 ON s2."SourceEntryId" = s3."Id"
    WHERE s."Id" = s2."TargetEntryId" AND NOT (s3."IsGmOnly")), s."UpdatedAt"
FROM "SettingEntries" AS s
WHERE s."CampaignSettingId" = 'bbbbbbbb-0000-0000-0000-000000000001' AND NOT (s."IsGmOnly")
  AND (s."Name" ILIKE '%harbor%' ESCAPE '\' OR (s."Description" IS NOT NULL AND s."Description" ILIKE '%harbor%' ESCAPE '\'))
ORDER BY s."Name", s."Id"
LIMIT 20 OFFSET 0;

\echo '== Q3 entry list, no search: first page =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT s."Id", s."Name", s."EntryType", s."Description", (
    SELECT count(*)::int FROM "SettingEntryRelationships" AS s0
    INNER JOIN "SettingEntries" AS s1 ON s0."TargetEntryId" = s1."Id"
    WHERE s."Id" = s0."SourceEntryId" AND NOT (s1."IsGmOnly")) + (
    SELECT count(*)::int FROM "SettingEntryRelationships" AS s2
    INNER JOIN "SettingEntries" AS s3 ON s2."SourceEntryId" = s3."Id"
    WHERE s."Id" = s2."TargetEntryId" AND NOT (s3."IsGmOnly")), s."UpdatedAt"
FROM "SettingEntries" AS s
WHERE s."CampaignSettingId" = 'bbbbbbbb-0000-0000-0000-000000000001' AND NOT (s."IsGmOnly")
ORDER BY s."Name", s."Id"
LIMIT 20 OFFSET 0;

\echo '== Q4 entry list, type filter: first page =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT s."Id", s."Name"
FROM "SettingEntries" AS s
WHERE s."CampaignSettingId" = 'bbbbbbbb-0000-0000-0000-000000000001' AND NOT (s."IsGmOnly") AND s."EntryType" = 'Location'
ORDER BY s."Name", s."Id"
LIMIT 20 OFFSET 0;

\echo '== Q5 dashboard: settings as GameMaster =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT count(*)::int
FROM "CampaignSettings" AS c
WHERE (c."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002' OR EXISTS (
    SELECT 1 FROM "SettingMemberships" AS s
    WHERE c."Id" = s."CampaignSettingId" AND s."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002'))
  AND (c."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002' OR EXISTS (
    SELECT 1 FROM "SettingMemberships" AS s0
    WHERE c."Id" = s0."CampaignSettingId" AND s0."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002' AND s0."Role" = 'GameMaster'));

\echo '== Q6 dashboard: characters by status =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT c."Status", count(*)::int AS "Count"
FROM "Characters" AS c
WHERE c."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002'
GROUP BY c."Status";

\echo '== Q7 dashboard: recent settings (5) =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT c0."Id", c0."Name", c0."Description", (
    SELECT count(*)::int FROM "SettingEntries" AS s0
    WHERE c0."Id" = s0."CampaignSettingId" AND (NOT (s0."IsGmOnly") OR c0."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002' OR EXISTS (
        SELECT 1 FROM "SettingMemberships" AS s1
        WHERE c0."Id" = s1."CampaignSettingId" AND s1."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002' AND s1."Role" = 'GameMaster'))), CASE
    WHEN c0."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002' THEN 'GameMaster'
    ELSE (SELECT s2."Role"::text FROM "SettingMemberships" AS s2
          WHERE c0."Id" = s2."CampaignSettingId" AND s2."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002' LIMIT 1)
END, c0."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002', a."DisplayName", c0."UpdatedAt"
FROM (
    SELECT c."Id", c."Description", c."Name", c."OwnerUserId", c."UpdatedAt"
    FROM "CampaignSettings" AS c
    WHERE c."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002' OR EXISTS (
        SELECT 1 FROM "SettingMemberships" AS s
        WHERE c."Id" = s."CampaignSettingId" AND s."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002')
    ORDER BY c."UpdatedAt" DESC, c."Id"
    LIMIT 5
) AS c0
INNER JOIN "AspNetUsers" AS a ON c0."OwnerUserId" = a."Id"
ORDER BY c0."UpdatedAt" DESC, c0."Id";

\echo '== Q8 dashboard: recent characters (5) =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT c1."Id", c1."Name", c1."Status", c1."CampaignSettingId", c0."Name", (
    SELECT CASE WHEN s."Id" IS NOT NULL THEN s."Name" ELSE c2."FreeText" END
    FROM "CharacterChoices" AS c2
    LEFT JOIN "SettingEntries" AS s ON c2."EntryId" = s."Id"
    WHERE c1."Id" = c2."CharacterId" AND c2."StepKey" = 'homeland'
    ORDER BY c2."Ordinal" LIMIT 1), c1."CurrentStep",
    c0."OwnerUserId" <> 'aaaaaaaa-0000-0000-0000-000000000002' AND NOT EXISTS (
    SELECT 1 FROM "SettingMemberships" AS s0
    WHERE c0."Id" = s0."CampaignSettingId" AND s0."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002'), c1."UpdatedAt"
FROM (
    SELECT c."Id", c."CampaignSettingId", c."CurrentStep", c."Name", c."Status", c."UpdatedAt"
    FROM "Characters" AS c
    WHERE c."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002'
    ORDER BY c."UpdatedAt" DESC, c."Id"
    LIMIT 5
) AS c1
INNER JOIN "CampaignSettings" AS c0 ON c1."CampaignSettingId" = c0."Id"
ORDER BY c1."UpdatedAt" DESC, c1."Id";

\echo '== Q9-old dashboard activity before P8-04: sort every visible entry (reference only) =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT s."Id", s."CampaignSettingId", s."Name", c0."Name" AS "SettingName", s."CreatedAt", s."UpdatedAt"
FROM "SettingEntries" AS s
INNER JOIN "CampaignSettings" AS c0 ON s."CampaignSettingId" = c0."Id"
WHERE EXISTS (
    SELECT 1 FROM "CampaignSettings" AS c
    WHERE (c."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002' OR EXISTS (
        SELECT 1 FROM "SettingMemberships" AS s0
        WHERE c."Id" = s0."CampaignSettingId" AND s0."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002'))
      AND c."Id" = s."CampaignSettingId")
  AND (NOT (s."IsGmOnly") OR c0."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002' OR EXISTS (
    SELECT 1 FROM "SettingMemberships" AS s1
    WHERE c0."Id" = s1."CampaignSettingId" AND s1."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002' AND s1."Role" = 'GameMaster'))
ORDER BY s."UpdatedAt" DESC
LIMIT 10;

\echo '== Q9 dashboard activity: recent visible entries (10), newest 10 per setting (LATERAL) =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT s2."Id", s2."CampaignSettingId", s2."Name", s2."SettingName", s2."CreatedAt", s2."UpdatedAt"
FROM "CampaignSettings" AS c
JOIN LATERAL (
    SELECT s0."Id", s0."CampaignSettingId", s0."Name", c."Name" AS "SettingName", s0."CreatedAt", s0."UpdatedAt"
    FROM "SettingEntries" AS s0
    WHERE c."Id" = s0."CampaignSettingId" AND (NOT (s0."IsGmOnly") OR c."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002' OR EXISTS (
        SELECT 1 FROM "SettingMemberships" AS s1
        WHERE c."Id" = s1."CampaignSettingId" AND s1."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002' AND s1."Role" = 'GameMaster'))
    ORDER BY s0."UpdatedAt" DESC
    LIMIT 10
) AS s2 ON TRUE
WHERE c."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002' OR EXISTS (
    SELECT 1 FROM "SettingMemberships" AS s
    WHERE c."Id" = s."CampaignSettingId" AND s."UserId" = 'aaaaaaaa-0000-0000-0000-000000000002')
ORDER BY s2."UpdatedAt" DESC
LIMIT 10;

\echo '== Q10 dashboard activity: recent characters (10) =='
EXPLAIN (ANALYZE, BUFFERS, COSTS OFF)
SELECT c."Id", c."CampaignSettingId", c."Name", c."CreatedAt", c."UpdatedAt"
FROM "Characters" AS c
WHERE c."OwnerUserId" = 'aaaaaaaa-0000-0000-0000-000000000002'
ORDER BY c."UpdatedAt" DESC
LIMIT 10;
