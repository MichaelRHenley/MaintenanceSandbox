using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceSandbox.Migrations
{
    /// <inheritdoc />
    public partial class TenantFilterForMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "MaintenanceRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "MaintenanceRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "MaintenanceRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "MaintenanceMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardedAtUtc",
                table: "MaintenanceMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardedByUserId",
                table: "MaintenanceMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MaintenanceMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "MaintenanceMessages");

            migrationBuilder.DropColumn(
                name: "OnboardedAtUtc",
                table: "MaintenanceMessages");

            migrationBuilder.DropColumn(
                name: "OnboardedByUserId",
                table: "MaintenanceMessages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MaintenanceMessages");
        }
    }
}
