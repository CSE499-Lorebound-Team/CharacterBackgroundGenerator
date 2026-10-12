using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lorebound.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationshipConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettingEntryRelationships_SourceEntryId",
                table: "SettingEntryRelationships");

            migrationBuilder.AlterColumn<string>(
                name: "RelationshipType",
                table: "SettingEntryRelationships",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "SettingEntryRelationships",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettingEntryRelationships_Source_Target_Type",
                table: "SettingEntryRelationships",
                columns: new[] { "SourceEntryId", "TargetEntryId", "RelationshipType" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SettingEntryRelationships_NoSelfLink",
                table: "SettingEntryRelationships",
                sql: "\"SourceEntryId\" <> \"TargetEntryId\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettingEntryRelationships_Source_Target_Type",
                table: "SettingEntryRelationships");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SettingEntryRelationships_NoSelfLink",
                table: "SettingEntryRelationships");

            migrationBuilder.AlterColumn<string>(
                name: "RelationshipType",
                table: "SettingEntryRelationships",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "SettingEntryRelationships",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettingEntryRelationships_SourceEntryId",
                table: "SettingEntryRelationships",
                column: "SourceEntryId");
        }
    }
}
