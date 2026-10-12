using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lorebound.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettingInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignSettingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MaxUses = table.Column<int>(type: "integer", nullable: true),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingInvites", x => x.Id);
                    table.CheckConstraint("CK_SettingInvites_MaxUses", "\"MaxUses\" IS NULL OR \"MaxUses\" > 0");
                    table.CheckConstraint("CK_SettingInvites_UseCount", "\"UseCount\" >= 0 AND (\"MaxUses\" IS NULL OR \"UseCount\" <= \"MaxUses\")");
                    table.ForeignKey(
                        name: "FK_SettingInvites_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettingInvites_CampaignSettings_CampaignSettingId",
                        column: x => x.CampaignSettingId,
                        principalTable: "CampaignSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettingInvites_CampaignSettingId",
                table: "SettingInvites",
                column: "CampaignSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_SettingInvites_Code",
                table: "SettingInvites",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettingInvites_CreatedByUserId",
                table: "SettingInvites",
                column: "CreatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettingInvites");
        }
    }
}
