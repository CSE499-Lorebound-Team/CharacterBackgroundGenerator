using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lorebound.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryVisibilityAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettingEntries_CampaignSettingId",
                table: "SettingEntries");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "SettingEntries",
                type: "citext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "IsGmOnly",
                table: "SettingEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SettingEntries_CampaignSettingId_EntryType",
                table: "SettingEntries",
                columns: new[] { "CampaignSettingId", "EntryType" });

            migrationBuilder.CreateIndex(
                name: "IX_SettingEntries_CampaignSettingId_EntryType_Name",
                table: "SettingEntries",
                columns: new[] { "CampaignSettingId", "EntryType", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettingEntries_CampaignSettingId_IsGmOnly",
                table: "SettingEntries",
                columns: new[] { "CampaignSettingId", "IsGmOnly" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettingEntries_CampaignSettingId_EntryType",
                table: "SettingEntries");

            migrationBuilder.DropIndex(
                name: "IX_SettingEntries_CampaignSettingId_EntryType_Name",
                table: "SettingEntries");

            migrationBuilder.DropIndex(
                name: "IX_SettingEntries_CampaignSettingId_IsGmOnly",
                table: "SettingEntries");

            migrationBuilder.DropColumn(
                name: "IsGmOnly",
                table: "SettingEntries");

            // The column must stop using citext before the extension can go.
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "SettingEntries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_SettingEntries_CampaignSettingId",
                table: "SettingEntries",
                column: "CampaignSettingId");
        }
    }
}
