using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class RunnerConfiguration : IEntityTypeConfiguration<Runner>
{
    public void Configure(EntityTypeBuilder<Runner> builder)
    {
        builder.ToTable("runners", table =>
        {
            table.HasCheckConstraint("ck_runners_numbers", "cloth_number > 0 AND (draw IS NULL OR draw > 0) AND (carried_weight_pounds IS NULL OR carried_weight_pounds > 0)");
            table.HasCheckConstraint("ck_runners_odds", "declared_odds IS NULL OR declared_odds > 1");
            table.HasCheckConstraint("ck_runners_status", "(status = 'Declared' AND non_runner_reason IS NULL) OR (status = 'NonRunner' AND length(trim(non_runner_reason)) > 0 AND non_runner_reason IS NOT NULL)");
        });
        builder.HasKey(runner => runner.Id).HasName("pk_runners");
        builder.HasAlternateKey(runner => new { runner.Id, runner.RaceId }).HasName("ak_runners_id_race_id");

        builder.Property(runner => runner.Id).HasColumnName("id");
        builder.Property(runner => runner.RaceId).HasColumnName("race_id");
        builder.Property(runner => runner.HorseId).HasColumnName("horse_id");
        builder.Property(runner => runner.TrainerId).HasColumnName("trainer_id");
        builder.Property(runner => runner.StableId).HasColumnName("stable_id");
        builder.Property(runner => runner.OwnerId).HasColumnName("owner_id");
        builder.Property(runner => runner.JockeyId).HasColumnName("jockey_id");
        builder.Property(runner => runner.ClothNumber).HasColumnName("cloth_number");
        builder.Property(runner => runner.Draw).HasColumnName("draw");
        builder.Property(runner => runner.CarriedWeightPounds)
            .HasColumnName("carried_weight_pounds");
        builder.Property(runner => runner.DeclaredOdds)
            .HasColumnName("declared_odds")
            .HasPrecision(10, 4);
        builder.Property(runner => runner.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(runner => runner.NonRunnerReason)
            .HasColumnName("non_runner_reason")
            .HasMaxLength(500);

        builder.HasOne(runner => runner.Race)
            .WithMany(race => race.Runners)
            .HasForeignKey(runner => runner.RaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_runners_races_race_id");
        builder.HasOne(runner => runner.Horse)
            .WithMany()
            .HasForeignKey(runner => runner.HorseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_runners_horses_horse_id");
        builder.HasOne(runner => runner.Trainer)
            .WithMany()
            .HasForeignKey(runner => runner.TrainerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_runners_trainers_trainer_id");
        builder.HasOne(runner => runner.Owner)
            .WithMany()
            .HasForeignKey(runner => runner.OwnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_runners_owners_owner_id");
        builder.HasOne(runner => runner.Stable)
            .WithMany()
            .HasForeignKey(runner => runner.StableId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_runners_stables_stable_id");
        builder.HasOne(runner => runner.Jockey)
            .WithMany()
            .HasForeignKey(runner => runner.JockeyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_runners_jockeys_jockey_id");

        builder.HasIndex(runner => new { runner.RaceId, runner.ClothNumber })
            .IsUnique()
            .HasDatabaseName("ux_runners_race_cloth_number");
        builder.HasIndex(runner => new { runner.RaceId, runner.HorseId })
            .IsUnique()
            .HasDatabaseName("ux_runners_race_horse");
        builder.HasIndex(runner => runner.HorseId)
            .HasDatabaseName("ix_runners_horse_id");
        builder.HasIndex(runner => runner.TrainerId)
            .HasDatabaseName("ix_runners_trainer_id");
        builder.HasIndex(runner => runner.OwnerId)
            .HasDatabaseName("ix_runners_owner_id");
        builder.HasIndex(runner => runner.JockeyId)
            .HasDatabaseName("ix_runners_jockey_id");
        builder.HasIndex(runner => new { runner.RaceId, runner.Draw })
            .IsUnique()
            .HasFilter("draw IS NOT NULL")
            .HasDatabaseName("ux_runners_race_draw");
    }
}
