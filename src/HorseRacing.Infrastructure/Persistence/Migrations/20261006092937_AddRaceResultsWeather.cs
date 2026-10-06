using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceResultsWeather : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "racecourse_locations",
                schema: "curated",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_course_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    course_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    postcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    location_source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    raw_payload_id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_collection_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_racecourse_locations", x => x.id);
                    table.CheckConstraint("ck_curated_racecourse_locations_latitude", "latitude BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_curated_racecourse_locations_longitude", "longitude BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "fk_curated_racecourse_locations_raw_collection_runs_raw_collection_run_id",
                        column: x => x.raw_collection_run_id,
                        principalSchema: "raw",
                        principalTable: "collection_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_curated_racecourse_locations_raw_payloads_raw_payload_id",
                        column: x => x.raw_payload_id,
                        principalSchema: "raw",
                        principalTable: "payloads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "race_weather",
                schema: "curated",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    curated_race_id = table.Column<Guid>(type: "uuid", nullable: false),
                    racecourse_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    race_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    weather_hour_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    temperature_c = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    apparent_temperature_c = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    relative_humidity_percent = table.Column<int>(type: "integer", nullable: false),
                    precipitation_millimetres = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    rain_millimetres = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    weather_code = table.Column<int>(type: "integer", nullable: false),
                    wind_speed_kilometres_per_hour = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    wind_direction_degrees = table.Column<int>(type: "integer", nullable: false),
                    wind_gust_kilometres_per_hour = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    raw_payload_id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_collection_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retrieved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_race_weather", x => x.id);
                    table.CheckConstraint("ck_curated_race_weather_code", "weather_code BETWEEN 0 AND 99");
                    table.CheckConstraint("ck_curated_race_weather_humidity", "relative_humidity_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_curated_race_weather_precipitation", "precipitation_millimetres >= 0 AND rain_millimetres >= 0");
                    table.CheckConstraint("ck_curated_race_weather_wind", "wind_speed_kilometres_per_hour >= 0 AND wind_gust_kilometres_per_hour >= 0 AND wind_direction_degrees BETWEEN 0 AND 360");
                    table.ForeignKey(
                        name: "fk_curated_race_weather_domain_objects_curated_race_id",
                        column: x => x.curated_race_id,
                        principalSchema: "curated",
                        principalTable: "domain_objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_curated_race_weather_locations_racecourse_location_id",
                        column: x => x.racecourse_location_id,
                        principalSchema: "curated",
                        principalTable: "racecourse_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_curated_race_weather_raw_collection_runs_raw_collection_run_id",
                        column: x => x.raw_collection_run_id,
                        principalSchema: "raw",
                        principalTable: "collection_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_curated_race_weather_raw_payloads_raw_payload_id",
                        column: x => x.raw_payload_id,
                        principalSchema: "raw",
                        principalTable: "payloads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_curated_race_weather_location_start",
                schema: "curated",
                table: "race_weather",
                columns: new[] { "racecourse_location_id", "race_start_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_curated_race_weather_raw_collection_run_id",
                schema: "curated",
                table: "race_weather",
                column: "raw_collection_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_curated_race_weather_raw_payload_id",
                schema: "curated",
                table: "race_weather",
                column: "raw_payload_id");

            migrationBuilder.CreateIndex(
                name: "ux_curated_race_weather_curated_race_id",
                schema: "curated",
                table: "race_weather",
                column: "curated_race_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_curated_racecourse_locations_raw_collection_run_id",
                schema: "curated",
                table: "racecourse_locations",
                column: "raw_collection_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_curated_racecourse_locations_raw_payload_id",
                schema: "curated",
                table: "racecourse_locations",
                column: "raw_payload_id");

            migrationBuilder.CreateIndex(
                name: "ux_curated_racecourse_locations_source_identity",
                schema: "curated",
                table: "racecourse_locations",
                columns: new[] { "source_system", "source_course_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "race_weather",
                schema: "curated");

            migrationBuilder.DropTable(
                name: "racecourse_locations",
                schema: "curated");
        }
    }
}
