using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceSandbox.Migrations
{
    /// <inheritdoc />
    public partial class AppDb_OnboardingDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "WorkCenters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "WorkCenters",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "WorkCenters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "Sites",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "Sites",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "Sites",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "Parts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "Parts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "Parts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "OnboardingSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "OnboardingSessions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "OnboardingSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "LocationBins",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "LocationBins",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "LocationBins",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "InventoryLevels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "InventoryLevels",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "InventoryLevels",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "Equipment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "Equipment",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "Equipment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "BomItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "BomItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "BomItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "Assets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "Assets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "Areas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "Areas",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "Areas",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "OnboardingSessions");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "OnboardingSessions");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "OnboardingSessions");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "LocationBins");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "LocationBins");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "LocationBins");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "InventoryLevels");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "InventoryLevels");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "InventoryLevels");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "BomItems");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "BomItems");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "BomItems");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "Areas");
        }
    }
}
