using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HorseRacingDbContext))]
[Migration("20261008090000_AddCuratedRaceDateIndex")]
public partial class AddCuratedRaceDateIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_curated_domain_objects_bha_race_date
            ON curated.domain_objects ((source_data -> 'foundData' ->> 'raceDate') DESC)
            WHERE source_system = 'BHA'
              AND domain_object_type = 'Race'
              AND (source_data -> 'foundData' ->> 'raceDate') IS NOT NULL;
            """,
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS curated.ix_curated_domain_objects_bha_race_date;",
            suppressTransaction: true);
    }
}
