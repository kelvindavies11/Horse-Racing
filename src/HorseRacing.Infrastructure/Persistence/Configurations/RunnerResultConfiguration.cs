using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class RunnerResultConfiguration : IEntityTypeConfiguration<RunnerResult>
{
    public void Configure(EntityTypeBuilder<RunnerResult> builder)
    {
        builder.ToTable("runner_results", table =>
        {
            table.HasCheckConstraint("ck_runner_results_outcome", "outcome IN ('Finished', 'PulledUp', 'Fell', 'UnseatedRider', 'Refused', 'BroughtDown', 'RanOut', 'Disqualified', 'Void')");
            table.HasCheckConstraint("ck_runner_results_position", "(outcome = 'Finished' AND finish_position IS NOT NULL AND finish_position > 0) OR (outcome <> 'Finished' AND finish_position IS NULL AND NOT is_dead_heat)");
            table.HasCheckConstraint("ck_runner_results_values", "(distance_beaten_lengths IS NULL OR distance_beaten_lengths >= 0) AND (starting_price_decimal IS NULL OR starting_price_decimal > 1) AND (prize_money IS NULL OR prize_money >= 0)");
        });
        builder.HasKey(result => result.Id).HasName("pk_runner_results");

        builder.Property(result => result.Id).HasColumnName("id");
        builder.Property(result => result.RaceResultId).HasColumnName("race_result_id");
        builder.Property(result => result.RaceId).HasColumnName("race_id");
        builder.Property(result => result.RunnerId).HasColumnName("runner_id");
        builder.Property(result => result.Outcome)
            .HasColumnName("outcome")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(result => result.FinishPosition).HasColumnName("finish_position");
        builder.Property(result => result.IsDeadHeat).HasColumnName("is_dead_heat");
        builder.Property(result => result.DistanceBeatenLengths)
            .HasColumnName("distance_beaten_lengths")
            .HasPrecision(8, 3);
        builder.Property(result => result.StartingPriceDecimal)
            .HasColumnName("starting_price_decimal")
            .HasPrecision(10, 4);
        builder.Property(result => result.PrizeMoney)
            .HasColumnName("prize_money")
            .HasPrecision(14, 2);

        builder.HasOne(result => result.RaceResult)
            .WithMany(raceResult => raceResult.RunnerResults)
            .HasForeignKey(result => new { result.RaceResultId, result.RaceId })
            .HasPrincipalKey(result => new { result.Id, result.RaceId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_runner_results_race_results_race_result_id");
        builder.HasOne(result => result.Runner)
            .WithOne()
            .HasForeignKey<RunnerResult>(result => new { result.RunnerId, result.RaceId })
            .HasPrincipalKey<Runner>(runner => new { runner.Id, runner.RaceId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_runner_results_runners_runner_id");

        builder.HasIndex(result => result.RunnerId)
            .IsUnique()
            .HasDatabaseName("ux_runner_results_runner_id");
        builder.HasIndex(result => new { result.RaceResultId, result.FinishPosition })
            .HasDatabaseName("ix_runner_results_result_finish_position");
    }
}
