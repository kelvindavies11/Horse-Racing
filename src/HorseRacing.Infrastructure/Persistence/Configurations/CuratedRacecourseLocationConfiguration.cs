using HorseRacing.Infrastructure.Ingestion.Weather;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

public sealed class CuratedRacecourseLocationConfiguration
    : IEntityTypeConfiguration<CuratedRacecourseLocation>
{
    public void Configure(EntityTypeBuilder<CuratedRacecourseLocation> builder)
    {
        builder.ToTable("racecourse_locations", "curated", table =>
        {
            table.HasCheckConstraint("ck_curated_racecourse_locations_latitude", "latitude BETWEEN -90 AND 90");
            table.HasCheckConstraint("ck_curated_racecourse_locations_longitude", "longitude BETWEEN -180 AND 180");
        });
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Id).HasColumnName("id");
        builder.Property(location => location.SourceSystem).HasColumnName("source_system").HasMaxLength(50).IsRequired();
        builder.Property(location => location.SourceCourseKey).HasColumnName("source_course_key").HasMaxLength(300).IsRequired();
        builder.Property(location => location.CourseName).HasColumnName("course_name").HasMaxLength(200).IsRequired();
        builder.Property(location => location.Postcode).HasColumnName("postcode").HasMaxLength(20);
        builder.Property(location => location.Latitude).HasColumnName("latitude").HasPrecision(9, 6).IsRequired();
        builder.Property(location => location.Longitude).HasColumnName("longitude").HasPrecision(9, 6).IsRequired();
        builder.Property(location => location.TimeZone).HasColumnName("time_zone").HasMaxLength(100).IsRequired();
        builder.Property(location => location.LocationSource).HasColumnName("location_source").HasMaxLength(100).IsRequired();
        builder.Property(location => location.SourceUrl).HasColumnName("source_url").HasMaxLength(2048).IsRequired();
        builder.Property(location => location.RawPayloadId).HasColumnName("raw_payload_id").IsRequired();
        builder.Property(location => location.RawCollectionRunId).HasColumnName("raw_collection_run_id").IsRequired();
        builder.Property(location => location.ResolvedAtUtc).HasColumnName("resolved_at_utc").IsRequired();

        builder.HasIndex(location => new { location.SourceSystem, location.SourceCourseKey })
            .IsUnique()
            .HasDatabaseName("ux_curated_racecourse_locations_source_identity");
        builder.HasIndex(location => location.RawPayloadId)
            .HasDatabaseName("ix_curated_racecourse_locations_raw_payload_id");
        builder.HasIndex(location => location.RawCollectionRunId)
            .HasDatabaseName("ix_curated_racecourse_locations_raw_collection_run_id");

        builder.HasOne(location => location.RawPayload)
            .WithMany()
            .HasForeignKey(location => location.RawPayloadId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_racecourse_locations_raw_payloads_raw_payload_id");
        builder.HasOne(location => location.RawCollectionRun)
            .WithMany()
            .HasForeignKey(location => location.RawCollectionRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_racecourse_locations_raw_collection_runs_raw_collection_run_id");
    }
}
