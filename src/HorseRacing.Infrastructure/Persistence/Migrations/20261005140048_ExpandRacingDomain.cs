using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandRacingDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The scaffold did not capture meeting identity, distance, or race-time connections.
            // Fail before DDL rather than inventing these facts for existing races.
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM races) OR EXISTS (SELECT 1 FROM runners) THEN
                        RAISE EXCEPTION 'ExpandRacingDomain requires empty races and runners. Back up and design a source-backed legacy mapping before upgrading a populated scaffold.';
                    END IF;
                END
                $guard$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_races_racecourses_racecourse_id",
                table: "races");

            migrationBuilder.RenameColumn(
                name: "racecourse_id",
                table: "races",
                newName: "meeting_id");

            migrationBuilder.RenameIndex(
                name: "ix_races_racecourse_start",
                table: "races",
                newName: "ix_races_meeting_start");

            migrationBuilder.AddColumn<int>(
                name: "carried_weight_pounds",
                table: "runners",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "draw",
                table: "runners",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "jockey_id",
                table: "runners",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "non_runner_reason",
                table: "runners",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "runners",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "stable_id",
                table: "runners",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "runners",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "trainer_id",
                table: "runners",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "races",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "distance_metres",
                table: "races",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "going_description",
                table: "races",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "race_number",
                table: "races",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "surface",
                table: "races",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_runners_id_race_id",
                table: "runners",
                columns: new[] { "id", "race_id" });

            migrationBuilder.CreateTable(
                name: "jockeys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    licence_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jockeys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "meetings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    racecourse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meetings", x => x.id);
                    table.CheckConstraint("ck_meetings_status", "status IN ('Scheduled', 'InProgress', 'Completed', 'Abandoned')");
                    table.CheckConstraint("ck_meetings_type", "type IN ('Flat', 'Jump', 'Mixed')");
                    table.ForeignKey(
                        name: "fk_meetings_racecourses_racecourse_id",
                        column: x => x.racecourse_id,
                        principalTable: "racecourses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "owners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_owners", x => x.id);
                    table.CheckConstraint("ck_owners_type", "type IN ('Individual', 'Partnership', 'Syndicate', 'Company', 'RacingClub')");
                });

            migrationBuilder.CreateTable(
                name: "race_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    race_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    winning_time = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_race_results", x => x.id);
                    table.UniqueConstraint("ak_race_results_id_race_id", x => new { x.id, x.race_id });
                    table.CheckConstraint("ck_race_results_status", "status IN ('Provisional', 'Official')");
                    table.CheckConstraint("ck_race_results_time", "winning_time IS NULL OR winning_time > interval '0 seconds'");
                    table.ForeignKey(
                        name: "fk_race_results_races_race_id",
                        column: x => x.race_id,
                        principalTable: "races",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    town = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stables", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "runner_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    race_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    race_id = table.Column<Guid>(type: "uuid", nullable: false),
                    runner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    finish_position = table.Column<int>(type: "integer", nullable: true),
                    is_dead_heat = table.Column<bool>(type: "boolean", nullable: false),
                    distance_beaten_lengths = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    starting_price_decimal = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    prize_money = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_runner_results", x => x.id);
                    table.CheckConstraint("ck_runner_results_outcome", "outcome IN ('Finished', 'PulledUp', 'Fell', 'UnseatedRider', 'Refused', 'BroughtDown', 'RanOut', 'Disqualified', 'Void')");
                    table.CheckConstraint("ck_runner_results_position", "(outcome = 'Finished' AND finish_position IS NOT NULL AND finish_position > 0) OR (outcome <> 'Finished' AND finish_position IS NULL AND NOT is_dead_heat)");
                    table.CheckConstraint("ck_runner_results_values", "(distance_beaten_lengths IS NULL OR distance_beaten_lengths >= 0) AND (starting_price_decimal IS NULL OR starting_price_decimal > 1) AND (prize_money IS NULL OR prize_money >= 0)");
                    table.ForeignKey(
                        name: "fk_runner_results_race_results_race_result_id",
                        columns: x => new { x.race_result_id, x.race_id },
                        principalTable: "race_results",
                        principalColumns: new[] { "id", "race_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_runner_results_runners_runner_id",
                        columns: x => new { x.runner_id, x.race_id },
                        principalTable: "runners",
                        principalColumns: new[] { "id", "race_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trainers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    licence_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trainers", x => x.id);
                    table.ForeignKey(
                        name: "fk_trainers_stables_stable_id",
                        column: x => x.stable_id,
                        principalTable: "stables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_runners_jockey_id",
                table: "runners",
                column: "jockey_id");

            migrationBuilder.CreateIndex(
                name: "ix_runners_owner_id",
                table: "runners",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_runners_stable_id",
                table: "runners",
                column: "stable_id");

            migrationBuilder.CreateIndex(
                name: "ix_runners_trainer_id",
                table: "runners",
                column: "trainer_id");

            migrationBuilder.CreateIndex(
                name: "ux_runners_race_draw",
                table: "runners",
                columns: new[] { "race_id", "draw" },
                unique: true,
                filter: "draw IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_runners_numbers",
                table: "runners",
                sql: "cloth_number > 0 AND (draw IS NULL OR draw > 0) AND (carried_weight_pounds IS NULL OR carried_weight_pounds > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_runners_odds",
                table: "runners",
                sql: "declared_odds IS NULL OR declared_odds > 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_runners_status",
                table: "runners",
                sql: "(status = 'Declared' AND non_runner_reason IS NULL) OR (status = 'NonRunner' AND length(trim(non_runner_reason)) > 0 AND non_runner_reason IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ux_races_meeting_race_number",
                table: "races",
                columns: new[] { "meeting_id", "race_number" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_races_code",
                table: "races",
                sql: "code IN ('Flat', 'Hurdle', 'Steeplechase', 'NationalHuntFlat')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_races_numbers",
                table: "races",
                sql: "race_number > 0 AND distance_metres > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_races_status",
                table: "races",
                sql: "status IN ('Scheduled', 'Off', 'Finished', 'Abandoned')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_races_surface",
                table: "races",
                sql: "surface IN ('Turf', 'AllWeather')");

            migrationBuilder.CreateIndex(
                name: "ux_jockeys_licence_number",
                table: "jockeys",
                columns: new[] { "country_code", "licence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meetings_racecourse_date_name",
                table: "meetings",
                columns: new[] { "racecourse_id", "scheduled_date", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_owners_identity",
                table: "owners",
                columns: new[] { "display_name", "type", "country_code" });

            migrationBuilder.CreateIndex(
                name: "ux_race_results_race_id",
                table: "race_results",
                column: "race_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_runner_results_race_result_id_race_id",
                table: "runner_results",
                columns: new[] { "race_result_id", "race_id" });

            migrationBuilder.CreateIndex(
                name: "ix_runner_results_result_finish_position",
                table: "runner_results",
                columns: new[] { "race_result_id", "finish_position" });

            migrationBuilder.CreateIndex(
                name: "IX_runner_results_runner_id_race_id",
                table: "runner_results",
                columns: new[] { "runner_id", "race_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_runner_results_runner_id",
                table: "runner_results",
                column: "runner_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stables_identity",
                table: "stables",
                columns: new[] { "name", "town", "country_code" });

            migrationBuilder.CreateIndex(
                name: "ix_trainers_stable_id",
                table: "trainers",
                column: "stable_id");

            migrationBuilder.CreateIndex(
                name: "ux_trainers_licence_number",
                table: "trainers",
                columns: new[] { "country_code", "licence_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_races_meetings_meeting_id",
                table: "races",
                column: "meeting_id",
                principalTable: "meetings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_runners_jockeys_jockey_id",
                table: "runners",
                column: "jockey_id",
                principalTable: "jockeys",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_runners_owners_owner_id",
                table: "runners",
                column: "owner_id",
                principalTable: "owners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_runners_stables_stable_id",
                table: "runners",
                column: "stable_id",
                principalTable: "stables",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_runners_trainers_trainer_id",
                table: "runners",
                column: "trainer_id",
                principalTable: "trainers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_races_meetings_meeting_id",
                table: "races");

            // Several meetings at a course may each contain a race numbered 1.
            migrationBuilder.DropIndex(
                name: "ux_races_meeting_race_number",
                table: "races");

            // Restore the original course references before removing meetings.
            migrationBuilder.Sql("""
                UPDATE races SET meeting_id = meetings.racecourse_id
                FROM meetings WHERE races.meeting_id = meetings.id;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_runners_jockeys_jockey_id",
                table: "runners");

            migrationBuilder.DropForeignKey(
                name: "fk_runners_owners_owner_id",
                table: "runners");

            migrationBuilder.DropForeignKey(
                name: "fk_runners_stables_stable_id",
                table: "runners");

            migrationBuilder.DropForeignKey(
                name: "fk_runners_trainers_trainer_id",
                table: "runners");

            migrationBuilder.DropTable(
                name: "jockeys");

            migrationBuilder.DropTable(
                name: "meetings");

            migrationBuilder.DropTable(
                name: "owners");

            migrationBuilder.DropTable(
                name: "runner_results");

            migrationBuilder.DropTable(
                name: "trainers");

            migrationBuilder.DropTable(
                name: "race_results");

            migrationBuilder.DropTable(
                name: "stables");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_runners_id_race_id",
                table: "runners");

            migrationBuilder.DropIndex(
                name: "ix_runners_jockey_id",
                table: "runners");

            migrationBuilder.DropIndex(
                name: "ix_runners_owner_id",
                table: "runners");

            migrationBuilder.DropIndex(
                name: "IX_runners_stable_id",
                table: "runners");

            migrationBuilder.DropIndex(
                name: "ix_runners_trainer_id",
                table: "runners");

            migrationBuilder.DropIndex(
                name: "ux_runners_race_draw",
                table: "runners");

            migrationBuilder.DropCheckConstraint(
                name: "ck_runners_numbers",
                table: "runners");

            migrationBuilder.DropCheckConstraint(
                name: "ck_runners_odds",
                table: "runners");

            migrationBuilder.DropCheckConstraint(
                name: "ck_runners_status",
                table: "runners");

            migrationBuilder.DropCheckConstraint(
                name: "ck_races_code",
                table: "races");

            migrationBuilder.DropCheckConstraint(
                name: "ck_races_numbers",
                table: "races");

            migrationBuilder.DropCheckConstraint(
                name: "ck_races_status",
                table: "races");

            migrationBuilder.DropCheckConstraint(
                name: "ck_races_surface",
                table: "races");

            migrationBuilder.DropColumn(
                name: "carried_weight_pounds",
                table: "runners");

            migrationBuilder.DropColumn(
                name: "draw",
                table: "runners");

            migrationBuilder.DropColumn(
                name: "jockey_id",
                table: "runners");

            migrationBuilder.DropColumn(
                name: "non_runner_reason",
                table: "runners");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "runners");

            migrationBuilder.DropColumn(
                name: "stable_id",
                table: "runners");

            migrationBuilder.DropColumn(
                name: "status",
                table: "runners");

            migrationBuilder.DropColumn(
                name: "trainer_id",
                table: "runners");

            migrationBuilder.DropColumn(
                name: "code",
                table: "races");

            migrationBuilder.DropColumn(
                name: "distance_metres",
                table: "races");

            migrationBuilder.DropColumn(
                name: "going_description",
                table: "races");

            migrationBuilder.DropColumn(
                name: "race_number",
                table: "races");

            migrationBuilder.DropColumn(
                name: "surface",
                table: "races");

            migrationBuilder.RenameColumn(
                name: "meeting_id",
                table: "races",
                newName: "racecourse_id");

            migrationBuilder.RenameIndex(
                name: "ix_races_meeting_start",
                table: "races",
                newName: "ix_races_racecourse_start");

            migrationBuilder.AddForeignKey(
                name: "fk_races_racecourses_racecourse_id",
                table: "races",
                column: "racecourse_id",
                principalTable: "racecourses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
