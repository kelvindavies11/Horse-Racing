using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Raw;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

public sealed class CuratedPromotionRunConfiguration
    : IEntityTypeConfiguration<CuratedPromotionRun>
{
    public void Configure(EntityTypeBuilder<CuratedPromotionRun> builder)
    {
        builder.ToTable(
            "promotion_runs",
            "curated",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_curated_promotion_runs_outcome",
                    "outcome IN ('Running', 'Succeeded', 'Skipped', 'Failed', 'Cancelled')");
                table.HasCheckConstraint(
                    "ck_curated_promotion_runs_completion",
                    "(outcome = 'Running' AND completed_at_utc IS NULL) OR " +
                    "(outcome <> 'Running' AND completed_at_utc IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_curated_promotion_runs_record_counts",
                    "records_found >= 0 AND records_upserted >= 0");
            });

        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(run => run.JobName).HasColumnName("job_name").HasMaxLength(100).IsRequired();
        builder.Property(run => run.PromoterVersion).HasColumnName("promoter_version").HasMaxLength(50).IsRequired();
        builder.Property(run => run.RawPayloadId).HasColumnName("raw_payload_id").IsRequired();
        builder.Property(run => run.RawCollectionRunId).HasColumnName("raw_collection_run_id").IsRequired();
        builder.Property(run => run.SourceJobName).HasColumnName("source_job_name").HasMaxLength(100).IsRequired();
        builder.Property(run => run.SourceName).HasColumnName("source_name").HasMaxLength(200).IsRequired();
        builder.Property(run => run.SourceUrl).HasColumnName("source_url").HasMaxLength(2048).IsRequired();
        builder.Property(run => run.PayloadSha256).HasColumnName("payload_sha256").HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(run => run.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(run => run.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(run => run.Outcome)
            .HasColumnName("outcome")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(run => run.RecordsFound).HasColumnName("records_found").IsRequired();
        builder.Property(run => run.RecordsUpserted).HasColumnName("records_upserted").IsRequired();
        builder.Property(run => run.ErrorCode).HasColumnName("error_code").HasMaxLength(100);
        builder.Property(run => run.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);

        builder.HasIndex(run => new { run.RawPayloadId, run.Outcome })
            .HasDatabaseName("ix_curated_promotion_runs_raw_payload_outcome");
        builder.HasIndex(run => run.RawCollectionRunId)
            .HasDatabaseName("ix_curated_promotion_runs_raw_collection_run_id");
        builder.HasIndex(run => new { run.SourceJobName, run.StartedAtUtc })
            .HasDatabaseName("ix_curated_promotion_runs_source_job_started");

        builder.HasOne(run => run.RawPayload)
            .WithMany()
            .HasForeignKey(run => run.RawPayloadId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_promotion_runs_raw_payloads_raw_payload_id");

        builder.HasOne(run => run.RawCollectionRun)
            .WithMany()
            .HasForeignKey(run => run.RawCollectionRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_promotion_runs_raw_collection_runs_raw_collection_run_id");
    }
}
