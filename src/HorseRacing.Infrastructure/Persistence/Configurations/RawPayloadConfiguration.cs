using HorseRacing.Infrastructure.Ingestion.Raw;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

public sealed class RawPayloadConfiguration : IEntityTypeConfiguration<RawPayload>
{
    public void Configure(EntityTypeBuilder<RawPayload> builder)
    {
        builder.ToTable(
            "payloads",
            "raw",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_raw_payloads_http_status_code",
                    "http_status_code BETWEEN 100 AND 599");
                table.HasCheckConstraint(
                    "ck_raw_payloads_sha256",
                    "sha256 ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_raw_payloads_content_length",
                    "content_length = octet_length(content)");
            });

        builder.HasKey(payload => payload.Id);
        builder.Property(payload => payload.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(payload => payload.CollectionRunId).HasColumnName("collection_run_id").IsRequired();
        builder.Property(payload => payload.SourceUrl).HasColumnName("source_url").HasMaxLength(2048).IsRequired();
        builder.Property(payload => payload.EffectiveUrl).HasColumnName("effective_url").HasMaxLength(2048).IsRequired();
        builder.Property(payload => payload.RetrievedAtUtc).HasColumnName("retrieved_at_utc").IsRequired();
        builder.Property(payload => payload.HttpStatusCode).HasColumnName("http_status_code").IsRequired();
        builder.Property(payload => payload.MediaType).HasColumnName("media_type").HasMaxLength(200);
        builder.Property(payload => payload.CharacterEncoding).HasColumnName("character_encoding").HasMaxLength(100);
        builder.Property(payload => payload.EntityTag).HasColumnName("entity_tag").HasMaxLength(500);
        builder.Property(payload => payload.LastModifiedUtc).HasColumnName("last_modified_utc");
        builder.Property(payload => payload.Sha256).HasColumnName("sha256").HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(payload => payload.ContentLength).HasColumnName("content_length").IsRequired();
        builder.Property(payload => payload.Content).HasColumnName("content").IsRequired();

        builder.HasIndex(payload => payload.CollectionRunId)
            .IsUnique()
            .HasDatabaseName("ux_raw_payloads_collection_run");
        builder.HasIndex(payload => payload.Sha256)
            .HasDatabaseName("ix_raw_payloads_sha256");
    }
}
