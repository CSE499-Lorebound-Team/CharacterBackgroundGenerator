using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lorebound.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixRelationshipDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SettingEntryRelationships_SettingEntries_SourceEntryId",
                table: "SettingEntryRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_SettingEntryRelationships_SettingEntries_TargetEntryId",
                table: "SettingEntryRelationships");

            migrationBuilder.AddForeignKey(
                name: "FK_SettingEntryRelationships_SettingEntries_SourceEntryId",
                table: "SettingEntryRelationships",
                column: "SourceEntryId",
                principalTable: "SettingEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SettingEntryRelationships_SettingEntries_TargetEntryId",
                table: "SettingEntryRelationships",
                column: "TargetEntryId",
                principalTable: "SettingEntries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SettingEntryRelationships_SettingEntries_SourceEntryId",
                table: "SettingEntryRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_SettingEntryRelationships_SettingEntries_TargetEntryId",
                table: "SettingEntryRelationships");

            migrationBuilder.AddForeignKey(
                name: "FK_SettingEntryRelationships_SettingEntries_SourceEntryId",
                table: "SettingEntryRelationships",
                column: "SourceEntryId",
                principalTable: "SettingEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SettingEntryRelationships_SettingEntries_TargetEntryId",
                table: "SettingEntryRelationships",
                column: "TargetEntryId",
                principalTable: "SettingEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
