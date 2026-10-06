using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCuratedPromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "curated");

            migrationBuilder.CreateTable(
                name: "promotion_runs",
                schema: "curated",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    promoter_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    raw_payload_id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_collection_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_job_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    payload_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    records_found = table.Column<int>(type: "integer", nullable: false),
                    records_upserted = table.Column<int>(type: "integer", nullable: false),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion_runs", x => x.id);
                    table.CheckConstraint("ck_curated_promotion_runs_completion", "(outcome = 'Running' AND completed_at_utc IS NULL) OR (outcome <> 'Running' AND completed_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_curated_promotion_runs_outcome", "outcome IN ('Running', 'Succeeded', 'Skipped', 'Failed', 'Cancelled')");
                    table.CheckConstraint("ck_curated_promotion_runs_record_counts", "records_found >= 0 AND records_upserted >= 0");
                    table.ForeignKey(
                        name: "fk_curated_promotion_runs_raw_collection_runs_raw_collection_run_id",
                        column: x => x.raw_collection_run_id,
                        principalSchema: "raw",
                        principalTable: "collection_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_curated_promotion_runs_raw_payloads_raw_payload_id",
                        column: x => x.raw_payload_id,
                        principalSchema: "raw",
                        principalTable: "payloads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "domain_objects",
                schema: "curated",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    domain_object_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    display_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    raw_payload_id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_collection_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_promotion_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_data = table.Column<string>(type: "jsonb", nullable: false),
                    first_observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_objects", x => x.id);
                    table.CheckConstraint("ck_curated_domain_objects_observation_dates", "last_observed_at_utc >= first_observed_at_utc");
                    table.CheckConstraint("ck_curated_domain_objects_source_data_json", "jsonb_typeof(source_data) = 'object'");
                    table.ForeignKey(
                        name: "fk_curated_domain_objects_promotion_runs_last_promotion_run_id",
                        column: x => x.last_promotion_run_id,
                        principalSchema: "curated",
                        principalTable: "promotion_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_curated_domain_objects_raw_collection_runs_raw_collection_run_id",
                        column: x => x.raw_collection_run_id,
                        principalSchema: "raw",
                        principalTable: "collection_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_curated_domain_objects_raw_payloads_raw_payload_id",
                        column: x => x.raw_payload_id,
                        principalSchema: "raw",
                        principalTable: "payloads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_curated_domain_objects_last_promotion_run_id",
                schema: "curated",
                table: "domain_objects",
                column: "last_promotion_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_curated_domain_objects_raw_collection_run_id",
                schema: "curated",
                table: "domain_objects",
                column: "raw_collection_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_curated_domain_objects_raw_payload_id",
                schema: "curated",
                table: "domain_objects",
                column: "raw_payload_id");

            migrationBuilder.CreateIndex(
                name: "ix_curated_domain_objects_type_display_name",
                schema: "curated",
                table: "domain_objects",
                columns: new[] { "domain_object_type", "display_name" });

            migrationBuilder.CreateIndex(
                name: "ux_curated_domain_objects_source_identity",
                schema: "curated",
                table: "domain_objects",
                columns: new[] { "source_system", "domain_object_type", "source_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_curated_promotion_runs_raw_collection_run_id",
                schema: "curated",
                table: "promotion_runs",
                column: "raw_collection_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_curated_promotion_runs_raw_payload_outcome",
                schema: "curated",
                table: "promotion_runs",
                columns: new[] { "raw_payload_id", "outcome" });

            migrationBuilder.CreateIndex(
                name: "ix_curated_promotion_runs_source_job_started",
                schema: "curated",
                table: "promotion_runs",
                columns: new[] { "source_job_name", "started_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "domain_objects",
                schema: "curated");

            migrationBuilder.DropTable(
                name: "promotion_runs",
                schema: "curated");
        }
    }
}
