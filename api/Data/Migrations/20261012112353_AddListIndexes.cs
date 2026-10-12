using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lorebound.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Characters_OwnerUserId",
                table: "Characters");

            migrationBuilder.CreateIndex(
                name: "IX_SettingEntries_CampaignSettingId_Name_Id",
                table: "SettingEntries",
                columns: new[] { "CampaignSettingId", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SettingEntries_CampaignSettingId_UpdatedAt",
                table: "SettingEntries",
                columns: new[] { "CampaignSettingId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Characters_OwnerUserId_UpdatedAt",
                table: "Characters",
                columns: new[] { "OwnerUserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettingEntries_CampaignSettingId_Name_Id",
                table: "SettingEntries");

            migrationBuilder.DropIndex(
                name: "IX_SettingEntries_CampaignSettingId_UpdatedAt",
                table: "SettingEntries");

            migrationBuilder.DropIndex(
                name: "IX_Characters_OwnerUserId_UpdatedAt",
                table: "Characters");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_OwnerUserId",
                table: "Characters",
                column: "OwnerUserId");
        }
    }
}
