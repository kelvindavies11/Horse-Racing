using HorseRacing.Infrastructure.Ingestion.Curated;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

public sealed class CuratedDomainObjectConfiguration
    : IEntityTypeConfiguration<CuratedDomainObject>
{
    public void Configure(EntityTypeBuilder<CuratedDomainObject> builder)
    {
        builder.ToTable(
            "domain_objects",
            "curated",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_curated_domain_objects_observation_dates",
                    "last_observed_at_utc >= first_observed_at_utc");
                table.HasCheckConstraint(
                    "ck_curated_domain_objects_source_data_json",
                    "jsonb_typeof(source_data) = 'object'");
            });

        builder.HasKey(domainObject => domainObject.Id);
        builder.Property(domainObject => domainObject.Id).HasColumnName("id");
        builder.Property(domainObject => domainObject.SourceSystem).HasColumnName("source_system").HasMaxLength(50).IsRequired();
        builder.Property(domainObject => domainObject.DomainObjectType).HasColumnName("domain_object_type").HasMaxLength(100).IsRequired();
        builder.Property(domainObject => domainObject.SourceKey).HasColumnName("source_key").HasMaxLength(300).IsRequired();
        builder.Property(domainObject => domainObject.DisplayName).HasColumnName("display_name").HasMaxLength(500).IsRequired();
        builder.Property(domainObject => domainObject.SourceUrl).HasColumnName("source_url").HasMaxLength(2048).IsRequired();
        builder.Property(domainObject => domainObject.RawPayloadId).HasColumnName("raw_payload_id").IsRequired();
        builder.Property(domainObject => domainObject.RawCollectionRunId).HasColumnName("raw_collection_run_id").IsRequired();
        builder.Property(domainObject => domainObject.LastPromotionRunId).HasColumnName("last_promotion_run_id").IsRequired();
        builder.Property(domainObject => domainObject.SourceDataJson)
            .HasColumnName("source_data")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(domainObject => domainObject.FirstObservedAtUtc).HasColumnName("first_observed_at_utc").IsRequired();
        builder.Property(domainObject => domainObject.LastObservedAtUtc).HasColumnName("last_observed_at_utc").IsRequired();

        builder.HasIndex(domainObject => new
            {
                domainObject.SourceSystem,
                domainObject.DomainObjectType,
                domainObject.SourceKey
            })
            .IsUnique()
            .HasDatabaseName("ux_curated_domain_objects_source_identity");
        builder.HasIndex(domainObject => new
            {
                domainObject.DomainObjectType,
                domainObject.DisplayName
            })
            .HasDatabaseName("ix_curated_domain_objects_type_display_name");
        builder.HasIndex(domainObject => domainObject.RawPayloadId)
            .HasDatabaseName("ix_curated_domain_objects_raw_payload_id");
        builder.HasIndex(domainObject => domainObject.RawCollectionRunId)
            .HasDatabaseName("ix_curated_domain_objects_raw_collection_run_id");
        builder.HasIndex(domainObject => domainObject.LastPromotionRunId)
            .HasDatabaseName("ix_curated_domain_objects_last_promotion_run_id");

        builder.HasOne(domainObject => domainObject.RawPayload)
            .WithMany()
            .HasForeignKey(domainObject => domainObject.RawPayloadId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_domain_objects_raw_payloads_raw_payload_id");

        builder.HasOne(domainObject => domainObject.RawCollectionRun)
            .WithMany()
            .HasForeignKey(domainObject => domainObject.RawCollectionRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_domain_objects_raw_collection_runs_raw_collection_run_id");

        builder.HasOne(domainObject => domainObject.LastPromotionRun)
            .WithMany()
            .HasForeignKey(domainObject => domainObject.LastPromotionRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_domain_objects_promotion_runs_last_promotion_run_id");
    }
}
