using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lorebound.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignSettingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    Backstory = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Characters_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Characters_CampaignSettings_CampaignSettingId",
                        column: x => x.CampaignSettingId,
                        principalTable: "CampaignSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterChoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    FreeText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterChoices", x => x.Id);
                    table.CheckConstraint("CK_CharacterChoices_EntryOrFreeText", "\"EntryId\" IS NULL OR \"FreeText\" IS NULL");
                    table.ForeignKey(
                        name: "FK_CharacterChoices_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterChoices_SettingEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "SettingEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterChoices_CharacterId_StepKey_Ordinal",
                table: "CharacterChoices",
                columns: new[] { "CharacterId", "StepKey", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterChoices_EntryId",
                table: "CharacterChoices",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_CampaignSettingId",
                table: "Characters",
                column: "CampaignSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_OwnerUserId",
                table: "Characters",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterChoices");

            migrationBuilder.DropTable(
                name: "Characters");
        }
    }
}
