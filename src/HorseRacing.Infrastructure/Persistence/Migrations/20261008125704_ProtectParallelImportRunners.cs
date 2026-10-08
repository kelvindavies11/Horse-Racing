using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectParallelImportRunners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_curated_promotion_runs_running_payload",
                schema: "curated",
                table: "promotion_runs",
                column: "raw_payload_id",
                unique: true,
                filter: "outcome = 'Running'");

            migrationBuilder.CreateIndex(
                name: "ux_raw_collection_runs_running_source",
                schema: "raw",
                table: "collection_runs",
                columns: new[] { "job_name", "source_url" },
                unique: true,
                filter: "outcome = 'Running'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_curated_promotion_runs_running_payload",
                schema: "curated",
                table: "promotion_runs");

            migrationBuilder.DropIndex(
                name: "ux_raw_collection_runs_running_source",
                schema: "raw",
                table: "collection_runs");
        }
    }
}
