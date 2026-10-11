using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HorseRacingDbContext))]
[Migration("20261011171000_AddAllSourceRaceDateIndex")]
public partial class AddAllSourceRaceDateIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_curated_domain_objects_all_source_race_date
            ON curated.domain_objects (((source_data -> 'foundData') ->> 'raceDate') DESC)
            INCLUDE (source_system, source_key)
            WHERE domain_object_type = 'Race'
              AND source_system IN ('BHA', 'KaggleRaceform')
              AND ((source_data -> 'foundData') ->> 'raceDate') IS NOT NULL;
            """,
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS curated.ix_curated_domain_objects_all_source_race_date;",
            suppressTransaction: true);
    }
}
