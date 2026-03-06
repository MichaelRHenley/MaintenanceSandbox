using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceSandbox.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentRequestsQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_WorkCenters_WorkCenterId",
                table: "Equipment");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Equipment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedUtc",
                table: "Equipment",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Equipment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EquipmentRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkCenterId = table.Column<int>(type: "int", nullable: false),
                    RequestedCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestedDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedByDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedEquipmentId = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsOnboarded = table.Column<bool>(type: "bit", nullable: false),
                    OnboardedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OnboardedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentRequests_WorkCenters_WorkCenterId",
                        column: x => x.WorkCenterId,
                        principalTable: "WorkCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRequests_TenantId_WorkCenterId_RequestedCode_Status",
                table: "EquipmentRequests",
                columns: new[] { "TenantId", "WorkCenterId", "RequestedCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRequests_TenantId_WorkCenterId_Status",
                table: "EquipmentRequests",
                columns: new[] { "TenantId", "WorkCenterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRequests_WorkCenterId",
                table: "EquipmentRequests",
                column: "WorkCenterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_WorkCenters_WorkCenterId",
                table: "Equipment",
                column: "WorkCenterId",
                principalTable: "WorkCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_WorkCenters_WorkCenterId",
                table: "Equipment");

            migrationBuilder.DropTable(
                name: "EquipmentRequests");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Equipment");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_WorkCenters_WorkCenterId",
                table: "Equipment",
                column: "WorkCenterId",
                principalTable: "WorkCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
