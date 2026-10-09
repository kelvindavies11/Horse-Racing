using HorseRacing.Infrastructure.Ingestion.Raw;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

public sealed class RaceResultsWorkQueueItemConfiguration
    : IEntityTypeConfiguration<RaceResultsWorkQueueItem>
{
    public void Configure(EntityTypeBuilder<RaceResultsWorkQueueItem> builder)
    {
        builder.ToTable(
            "result_work_queue",
            "raw",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_raw_result_work_queue_status",
                    "status IN ('Pending', 'Running', 'Succeeded', 'Unavailable', 'Failed')");
                table.HasCheckConstraint(
                    "ck_raw_result_work_queue_attempt_count",
                    "attempt_count >= 0");
            });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.DispatchItemId).HasColumnName("dispatch_item_id").HasMaxLength(50).IsRequired();
        builder.Property(item => item.WorkType).HasColumnName("work_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.JobName).HasColumnName("job_name").HasMaxLength(200).IsRequired();
        builder.Property(item => item.SourceName).HasColumnName("source_name").HasMaxLength(500).IsRequired();
        builder.Property(item => item.SourceUrl).HasColumnName("source_url").HasMaxLength(2048).IsRequired();
        builder.Property(item => item.Priority).HasColumnName("priority").IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.AvailableAtUtc).HasColumnName("available_at_utc").IsRequired();
        builder.Property(item => item.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(item => item.LastRawCollectionRunId).HasColumnName("last_raw_collection_run_id");
        builder.Property(item => item.LastRawPayloadId).HasColumnName("last_raw_payload_id");
        builder.Property(item => item.LastHttpStatusCode).HasColumnName("last_http_status_code");
        builder.Property(item => item.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(100);
        builder.Property(item => item.LastErrorMessage).HasColumnName("last_error_message").HasMaxLength(2000);
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(item => item.CompletedAtUtc).HasColumnName("completed_at_utc");

        builder.HasIndex(item => new { item.DispatchItemId, item.JobName, item.SourceUrl })
            .IsUnique()
            .HasDatabaseName("ux_raw_result_work_queue_dispatch_job_source");
        builder.HasIndex(item => new
            {
                item.DispatchItemId,
                item.Status,
                item.AvailableAtUtc,
                item.Priority,
                item.CreatedAtUtc
            })
            .HasDatabaseName("ix_raw_result_work_queue_claim");
    }
}
