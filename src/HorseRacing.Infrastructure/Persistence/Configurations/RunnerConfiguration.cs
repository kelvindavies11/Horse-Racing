using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class RunnerConfiguration : IEntityTypeConfiguration<Runner>
{
    public void Configure(EntityTypeBuilder<Runner> builder)
    {
        builder.ToTable("runners");
        builder.HasKey(runner => runner.Id).HasName("pk_runners");

        builder.Property(runner => runner.Id).HasColumnName("id");
        builder.Property(runner => runner.RaceId).HasColumnName("race_id");
        builder.Property(runner => runner.HorseId).HasColumnName("horse_id");
        builder.Property(runner => runner.ClothNumber).HasColumnName("cloth_number");
        builder.Property(runner => runner.DeclaredOdds)
            .HasColumnName("declared_odds")
            .HasPrecision(10, 4);

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

        builder.HasIndex(runner => new { runner.RaceId, runner.ClothNumber })
            .IsUnique()
            .HasDatabaseName("ux_runners_race_cloth_number");
        builder.HasIndex(runner => new { runner.RaceId, runner.HorseId })
            .IsUnique()
            .HasDatabaseName("ux_runners_race_horse");
        builder.HasIndex(runner => runner.HorseId)
            .HasDatabaseName("ix_runners_horse_id");
    }
}
