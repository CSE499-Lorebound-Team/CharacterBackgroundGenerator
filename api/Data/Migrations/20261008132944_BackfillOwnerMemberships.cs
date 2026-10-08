using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lorebound.Api.Data.Migrations
{
    /// <summary>
    /// Data only (P2-01). Settings created before memberships existed have no
    /// owner row, and rows from <c>AddSettingMembership</c> got '-infinity'
    /// for the columns <c>AddSettingMemberships</c> added.
    /// </summary>
    public partial class BackfillOwnerMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Placeholder timestamps: anything before year 2 is not a real time.
            migrationBuilder.Sql("""
                UPDATE "SettingMemberships"
                SET "JoinedAt" = "CreatedAt"
                WHERE "JoinedAt" < TIMESTAMPTZ '0002-01-01';

                UPDATE "SettingMemberships"
                SET "UpdatedAt" = "CreatedAt"
                WHERE "UpdatedAt" < TIMESTAMPTZ '0002-01-01';
                """);

            // The owner is always a GameMaster, never a Player.
            migrationBuilder.Sql("""
                UPDATE "SettingMemberships" AS m
                SET "Role" = 'GameMaster', "UpdatedAt" = now()
                FROM "CampaignSettings" AS s
                WHERE m."CampaignSettingId" = s."Id"
                  AND m."UserId" = s."OwnerUserId"
                  AND m."Role" <> 'GameMaster';
                """);

            // Every owner has a GameMaster membership row.
            migrationBuilder.Sql("""
                INSERT INTO "SettingMemberships"
                    ("Id", "CampaignSettingId", "UserId", "Role", "JoinedAt", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), s."Id", s."OwnerUserId", 'GameMaster', s."CreatedAt", now(), now()
                FROM "CampaignSettings" AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM "SettingMemberships" AS m
                    WHERE m."CampaignSettingId" = s."Id"
                      AND m."UserId" = s."OwnerUserId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the backfilled rows are indistinguishable from
            // ones the API created, and the schema did not change.
        }
    }
}
