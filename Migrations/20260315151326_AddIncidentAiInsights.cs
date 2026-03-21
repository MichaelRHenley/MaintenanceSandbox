using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceSandbox.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentAiInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IncidentEmbeddings is created at startup via EnsureRagTablesAsync (raw SQL).
            // Only create IncidentAiInsights here.
            migrationBuilder.CreateTable(
                name: "IncidentAiInsights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    InsightText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelUsed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentAiInsights", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAiInsights_TenantId_IncidentId",
                table: "IncidentAiInsights",
                columns: new[] { "TenantId", "IncidentId" },
                unique: true);

            // Ensure IncidentEmbeddings index exists (table created via EnsureRagTablesAsync).
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[dbo].[IncidentEmbeddings]')
                      AND name = N'IX_IncidentEmbeddings_TenantId_IncidentId'
                )
                CREATE UNIQUE INDEX [IX_IncidentEmbeddings_TenantId_IncidentId]
                    ON [dbo].[IncidentEmbeddings] ([TenantId], [IncidentId]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentAiInsights");
        }
    }
}
