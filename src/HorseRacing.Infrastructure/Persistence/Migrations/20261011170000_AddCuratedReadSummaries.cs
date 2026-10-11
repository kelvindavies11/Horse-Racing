using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HorseRacingDbContext))]
[Migration("20261011170000_AddCuratedReadSummaries")]
public partial class AddCuratedReadSummaries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE curated.domain_object_summaries (
                domain_object_type character varying(128) PRIMARY KEY,
                entity_count integer NOT NULL,
                first_observed_at_utc timestamp with time zone NOT NULL,
                last_observed_at_utc timestamp with time zone NOT NULL
            );

            INSERT INTO curated.domain_object_summaries (
                domain_object_type,
                entity_count,
                first_observed_at_utc,
                last_observed_at_utc)
            SELECT domain_object_type,
                   COUNT(*)::integer,
                   MIN(first_observed_at_utc),
                   MAX(last_observed_at_utc)
            FROM curated.domain_objects
            GROUP BY domain_object_type;

            CREATE OR REPLACE FUNCTION curated.maintain_domain_object_summary()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    INSERT INTO curated.domain_object_summaries (
                        domain_object_type, entity_count, first_observed_at_utc, last_observed_at_utc)
                    VALUES (NEW.domain_object_type, 1, NEW.first_observed_at_utc, NEW.last_observed_at_utc)
                    ON CONFLICT (domain_object_type) DO UPDATE
                    SET entity_count = curated.domain_object_summaries.entity_count + 1,
                        first_observed_at_utc = LEAST(
                            curated.domain_object_summaries.first_observed_at_utc,
                            EXCLUDED.first_observed_at_utc),
                        last_observed_at_utc = GREATEST(
                            curated.domain_object_summaries.last_observed_at_utc,
                            EXCLUDED.last_observed_at_utc);
                    RETURN NEW;
                END IF;

                IF TG_OP = 'UPDATE' AND OLD.domain_object_type = NEW.domain_object_type THEN
                    UPDATE curated.domain_object_summaries
                    SET first_observed_at_utc = LEAST(first_observed_at_utc, NEW.first_observed_at_utc),
                        last_observed_at_utc = GREATEST(last_observed_at_utc, NEW.last_observed_at_utc)
                    WHERE domain_object_type = NEW.domain_object_type;
                    RETURN NEW;
                END IF;

                UPDATE curated.domain_object_summaries summary
                SET entity_count = values.entity_count,
                    first_observed_at_utc = values.first_observed_at_utc,
                    last_observed_at_utc = values.last_observed_at_utc
                FROM (
                    SELECT COUNT(*)::integer AS entity_count,
                           MIN(first_observed_at_utc) AS first_observed_at_utc,
                           MAX(last_observed_at_utc) AS last_observed_at_utc
                    FROM curated.domain_objects
                    WHERE domain_object_type = OLD.domain_object_type
                      AND id <> OLD.id
                ) values
                WHERE summary.domain_object_type = OLD.domain_object_type
                  AND values.entity_count > 0;

                DELETE FROM curated.domain_object_summaries
                WHERE domain_object_type = OLD.domain_object_type
                  AND NOT EXISTS (
                      SELECT 1 FROM curated.domain_objects
                      WHERE domain_object_type = OLD.domain_object_type
                        AND id <> OLD.id);

                IF TG_OP = 'UPDATE' THEN
                    INSERT INTO curated.domain_object_summaries (
                        domain_object_type, entity_count, first_observed_at_utc, last_observed_at_utc)
                    VALUES (NEW.domain_object_type, 1, NEW.first_observed_at_utc, NEW.last_observed_at_utc)
                    ON CONFLICT (domain_object_type) DO UPDATE
                    SET entity_count = curated.domain_object_summaries.entity_count + 1,
                        first_observed_at_utc = LEAST(
                            curated.domain_object_summaries.first_observed_at_utc,
                            EXCLUDED.first_observed_at_utc),
                        last_observed_at_utc = GREATEST(
                            curated.domain_object_summaries.last_observed_at_utc,
                            EXCLUDED.last_observed_at_utc);
                    RETURN NEW;
                END IF;

                RETURN OLD;
            END;
            $$;

            CREATE TRIGGER maintain_domain_object_summary
            AFTER INSERT OR UPDATE OR DELETE ON curated.domain_objects
            FOR EACH ROW EXECUTE FUNCTION curated.maintain_domain_object_summary();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS maintain_domain_object_summary ON curated.domain_objects;
            DROP FUNCTION IF EXISTS curated.maintain_domain_object_summary();
            DROP TABLE IF EXISTS curated.domain_object_summaries;
            """);
    }
}
