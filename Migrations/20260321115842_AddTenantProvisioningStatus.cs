using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceSandbox.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProvisioningStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IncidentAiInsights_TenantId_IncidentId",
                table: "IncidentAiInsights");

            migrationBuilder.AddColumn<string>(
                name: "LastProvisioningError",
                table: "Tenants",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProvisionedAt",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProvisioningStatus",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "IncidentAiInsights",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAiInsights_TenantId_IncidentId_Language",
                table: "IncidentAiInsights",
                columns: new[] { "TenantId", "IncidentId", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IncidentAiInsights_TenantId_IncidentId_Language",
                table: "IncidentAiInsights");

            migrationBuilder.DropColumn(
                name: "LastProvisioningError",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ProvisionedAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ProvisioningStatus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "IncidentAiInsights");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAiInsights_TenantId_IncidentId",
                table: "IncidentAiInsights",
                columns: new[] { "TenantId", "IncidentId" },
                unique: true);
        }
    }
}
