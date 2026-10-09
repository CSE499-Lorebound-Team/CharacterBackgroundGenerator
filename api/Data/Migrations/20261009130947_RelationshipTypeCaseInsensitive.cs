using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lorebound.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RelationshipTypeCaseInsensitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RelationshipType",
                table: "SettingEntryRelationships",
                type: "citext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SettingEntryRelationships_RelationshipTypeLength",
                table: "SettingEntryRelationships",
                sql: "char_length(\"RelationshipType\") <= 60");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SettingEntryRelationships_RelationshipTypeLength",
                table: "SettingEntryRelationships");

            migrationBuilder.AlterColumn<string>(
                name: "RelationshipType",
                table: "SettingEntryRelationships",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext");
        }
    }
}
