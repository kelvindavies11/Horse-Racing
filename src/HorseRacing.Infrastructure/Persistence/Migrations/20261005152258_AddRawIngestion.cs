using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRawIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "raw");

            migrationBuilder.CreateTable(
                name: "collection_runs",
                schema: "raw",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    collector_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    http_status_code = table.Column<int>(type: "integer", nullable: true),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_runs", x => x.id);
                    table.CheckConstraint("ck_raw_collection_runs_completion", "(outcome = 'Running' AND completed_at_utc IS NULL) OR (outcome <> 'Running' AND completed_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_raw_collection_runs_outcome", "outcome IN ('Running', 'Succeeded', 'Failed', 'Cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "payloads",
                schema: "raw",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    effective_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    retrieved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    http_status_code = table.Column<int>(type: "integer", nullable: false),
                    media_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    character_encoding = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entity_tag = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_modified_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    content_length = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payloads", x => x.id);
                    table.CheckConstraint("ck_raw_payloads_content_length", "content_length = octet_length(content)");
                    table.CheckConstraint("ck_raw_payloads_http_status_code", "http_status_code BETWEEN 100 AND 599");
                    table.CheckConstraint("ck_raw_payloads_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_payloads_collection_runs_collection_run_id",
                        column: x => x.collection_run_id,
                        principalSchema: "raw",
                        principalTable: "collection_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_raw_collection_runs_source_started",
                schema: "raw",
                table: "collection_runs",
                columns: new[] { "source_url", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_raw_payloads_sha256",
                schema: "raw",
                table: "payloads",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "ux_raw_payloads_collection_run",
                schema: "raw",
                table: "payloads",
                column: "collection_run_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payloads",
                schema: "raw");

            migrationBuilder.DropTable(
                name: "collection_runs",
                schema: "raw");
        }
    }
}
