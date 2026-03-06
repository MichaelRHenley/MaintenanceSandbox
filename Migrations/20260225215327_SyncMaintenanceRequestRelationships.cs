using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceSandbox.Migrations
{
    /// <inheritdoc />
    public partial class SyncMaintenanceRequestRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Equipment",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "WorkCenter",
                table: "MaintenanceRequests");         
                      

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_EquipmentId",
                table: "MaintenanceRequests",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_TenantId_WorkCenterId",
                table: "MaintenanceRequests",
                columns: new[] { "TenantId", "WorkCenterId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_WorkCenterId",
                table: "MaintenanceRequests",
                column: "WorkCenterId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_Equipment_EquipmentId",
                table: "MaintenanceRequests",
                column: "EquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_WorkCenters_WorkCenterId",
                table: "MaintenanceRequests",
                column: "WorkCenterId",
                principalTable: "WorkCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_Equipment_EquipmentId",
                table: "MaintenanceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_WorkCenters_WorkCenterId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_EquipmentId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_TenantId_WorkCenterId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_WorkCenterId",
                table: "MaintenanceRequests");          
            

            migrationBuilder.AddColumn<string>(
                name: "Equipment",
                table: "MaintenanceRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkCenter",
                table: "MaintenanceRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
