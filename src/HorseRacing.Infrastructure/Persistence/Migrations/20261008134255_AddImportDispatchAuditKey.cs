using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportDispatchAuditKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dispatch_item_id",
                schema: "raw",
                table: "collection_runs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE raw.collection_runs
                SET dispatch_item_id = regexp_replace(
                    job_name,
                    '^bha-results-fixtures-([0-9]{4})-([0-9]{2})-p[0-9]+$',
                    'year-\1:\1-\2')
                WHERE dispatch_item_id IS NULL
                    AND job_name ~ '^bha-results-fixtures-[0-9]{4}-[0-9]{2}-p[0-9]+$';

                UPDATE raw.collection_runs AS collection_run
                SET dispatch_item_id = 'year-' || left(meeting.source_key, 4) || ':' ||
                    left(meeting.source_data #>> '{foundData,fixtureDate}', 7)
                FROM curated.domain_objects AS meeting
                WHERE collection_run.dispatch_item_id IS NULL
                    AND collection_run.job_name ~ '^bha-results-races-[0-9]{4}-[0-9]+$'
                    AND meeting.source_system = 'BHA'
                    AND meeting.domain_object_type = 'Meeting'
                    AND meeting.source_key = regexp_replace(
                        collection_run.job_name,
                        '^bha-results-races-([0-9]{4})-([0-9]+)$',
                        '\1:\2');

                UPDATE raw.collection_runs AS collection_run
                SET dispatch_item_id = 'year-' || left(race.source_key, 4) || ':' ||
                    left(race.source_data #>> '{foundData,raceDate}', 7)
                FROM curated.domain_objects AS race
                WHERE collection_run.dispatch_item_id IS NULL
                    AND collection_run.job_name ~ '^bha-results-runners-[0-9]{4}-[0-9]+-[0-9]+$'
                    AND race.source_system = 'BHA'
                    AND race.domain_object_type = 'Race'
                    AND race.source_key = regexp_replace(
                        collection_run.job_name,
                        '^bha-results-runners-([0-9]{4})-([0-9]+)-([0-9]+)$',
                        '\1:\2:\3');
                """);

            migrationBuilder.CreateIndex(
                name: "ix_raw_collection_runs_dispatch_outcome",
                schema: "raw",
                table: "collection_runs",
                columns: new[] { "dispatch_item_id", "outcome" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_raw_collection_runs_dispatch_outcome",
                schema: "raw",
                table: "collection_runs");

            migrationBuilder.DropColumn(
                name: "dispatch_item_id",
                schema: "raw",
                table: "collection_runs");
        }
    }
}
