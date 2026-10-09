using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceResultsWorkQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "result_work_queue",
                schema: "raw",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_item_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    work_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    job_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    available_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_raw_collection_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_raw_payload_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_http_status_code = table.Column<int>(type: "integer", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_result_work_queue", x => x.id);
                    table.CheckConstraint("ck_raw_result_work_queue_attempt_count", "attempt_count >= 0");
                    table.CheckConstraint("ck_raw_result_work_queue_status", "status IN ('Pending', 'Running', 'Succeeded', 'Unavailable', 'Failed')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_raw_result_work_queue_claim",
                schema: "raw",
                table: "result_work_queue",
                columns: new[] { "dispatch_item_id", "status", "available_at_utc", "priority", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_raw_result_work_queue_dispatch_job_source",
                schema: "raw",
                table: "result_work_queue",
                columns: new[] { "dispatch_item_id", "job_name", "source_url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "result_work_queue",
                schema: "raw");
        }
    }
}
