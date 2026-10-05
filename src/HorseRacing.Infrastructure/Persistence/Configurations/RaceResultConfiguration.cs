using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class RaceResultConfiguration : IEntityTypeConfiguration<RaceResult>
{
    public void Configure(EntityTypeBuilder<RaceResult> builder)
    {
        builder.ToTable("race_results", table =>
        {
            table.HasCheckConstraint("ck_race_results_status", "status IN ('Provisional', 'Official')");
            table.HasCheckConstraint("ck_race_results_time", "winning_time IS NULL OR winning_time > interval '0 seconds'");
        });
        builder.HasKey(result => result.Id).HasName("pk_race_results");
        builder.HasAlternateKey(result => new { result.Id, result.RaceId }).HasName("ak_race_results_id_race_id");

        builder.Property(result => result.Id).HasColumnName("id");
        builder.Property(result => result.RaceId).HasColumnName("race_id");
        builder.Property(result => result.PublishedAtUtc)
            .HasColumnName("published_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(result => result.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(result => result.WinningTime)
            .HasColumnName("winning_time");

        builder.HasOne(result => result.Race)
            .WithOne(race => race.Result)
            .HasForeignKey<RaceResult>(result => result.RaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_race_results_races_race_id");

        builder.HasIndex(result => result.RaceId)
            .IsUnique()
            .HasDatabaseName("ux_race_results_race_id");

        builder.Navigation(result => result.RunnerResults)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
