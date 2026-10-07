using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lorebound.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SettingMemberships_AspNetUsers_UserId",
                table: "SettingMemberships");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "JoinedAt",
                table: "SettingMemberships",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "SettingMemberships",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddForeignKey(
                name: "FK_SettingMemberships_AspNetUsers_UserId",
                table: "SettingMemberships",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SettingMemberships_AspNetUsers_UserId",
                table: "SettingMemberships");

            migrationBuilder.DropColumn(
                name: "JoinedAt",
                table: "SettingMemberships");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SettingMemberships");

            migrationBuilder.AddForeignKey(
                name: "FK_SettingMemberships_AspNetUsers_UserId",
                table: "SettingMemberships",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
