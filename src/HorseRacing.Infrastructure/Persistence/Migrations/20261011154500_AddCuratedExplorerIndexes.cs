using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HorseRacingDbContext))]
[Migration("20261011154500_AddCuratedExplorerIndexes")]
public partial class AddCuratedExplorerIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS curated.ix_curated_domain_objects_type_observed;",
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_curated_domain_objects_type_observed
            ON curated.domain_objects (
                domain_object_type,
                last_observed_at_utc DESC,
                display_name)
            INCLUDE (first_observed_at_utc);
            """,
            suppressTransaction: true);

        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS curated.ix_curated_domain_objects_observed_display_name;",
            suppressTransaction: true);
        migrationBuilder.Sql(
            """
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_curated_domain_objects_observed_display_name
            ON curated.domain_objects (last_observed_at_utc DESC, display_name);
            """,
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS curated.ix_curated_domain_objects_observed_display_name;",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS curated.ix_curated_domain_objects_type_observed;",
            suppressTransaction: true);
    }
}
