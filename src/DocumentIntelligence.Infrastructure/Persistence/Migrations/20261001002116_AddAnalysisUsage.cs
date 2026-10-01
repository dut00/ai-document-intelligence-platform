using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisUsage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalyzedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisUsage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisUsage_AnalyzedAt",
                table: "AnalysisUsage",
                column: "AnalyzedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisUsage_DocumentId",
                table: "AnalysisUsage",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisUsage_OwnerId_AnalyzedAt",
                table: "AnalysisUsage",
                columns: new[] { "OwnerId", "AnalyzedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisUsage");
        }
    }
}
