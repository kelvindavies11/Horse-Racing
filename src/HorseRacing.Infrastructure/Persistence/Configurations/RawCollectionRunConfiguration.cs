using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Infrastructure.Ingestion.Raw;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

public sealed class RawCollectionRunConfiguration
    : IEntityTypeConfiguration<RawCollectionRun>
{
    public void Configure(EntityTypeBuilder<RawCollectionRun> builder)
    {
        builder.ToTable(
            "collection_runs",
            "raw",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_raw_collection_runs_outcome",
                    "outcome IN ('Running', 'Succeeded', 'Failed', 'Cancelled')");
                table.HasCheckConstraint(
                    "ck_raw_collection_runs_completion",
                    "(outcome = 'Running' AND completed_at_utc IS NULL) OR " +
                    "(outcome <> 'Running' AND completed_at_utc IS NOT NULL)");
            });

        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(run => run.JobName).HasColumnName("job_name").HasMaxLength(100).IsRequired();
        builder.Property(run => run.SourceName).HasColumnName("source_name").HasMaxLength(200).IsRequired();
        builder.Property(run => run.SourceUrl).HasColumnName("source_url").HasMaxLength(2048).IsRequired();
        builder.Property(run => run.CollectorVersion).HasColumnName("collector_version").HasMaxLength(50).IsRequired();
        builder.Property(run => run.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(run => run.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(run => run.Outcome)
            .HasColumnName("outcome")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(run => run.HttpStatusCode).HasColumnName("http_status_code");
        builder.Property(run => run.ErrorCode).HasColumnName("error_code").HasMaxLength(100);
        builder.Property(run => run.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);

        builder.HasIndex(run => new { run.SourceUrl, run.StartedAtUtc })
            .HasDatabaseName("ix_raw_collection_runs_source_started");
        builder.HasIndex(run => new { run.JobName, run.SourceUrl })
            .IsUnique()
            .HasFilter("outcome = 'Running'")
            .HasDatabaseName("ux_raw_collection_runs_running_source");

        builder.HasOne(run => run.Payload)
            .WithOne(payload => payload.CollectionRun)
            .HasForeignKey<RawPayload>(payload => payload.CollectionRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
