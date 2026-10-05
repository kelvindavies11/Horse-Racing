using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class RaceConfiguration : IEntityTypeConfiguration<Race>
{
    public void Configure(EntityTypeBuilder<Race> builder)
    {
        builder.ToTable("races");
        builder.HasKey(race => race.Id).HasName("pk_races");

        builder.Property(race => race.Id).HasColumnName("id");
        builder.Property(race => race.RacecourseId).HasColumnName("racecourse_id");
        builder.Property(race => race.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(race => race.ScheduledStartUtc)
            .HasColumnName("scheduled_start_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(race => race.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(race => race.Racecourse)
            .WithMany()
            .HasForeignKey(race => race.RacecourseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_races_racecourses_racecourse_id");

        builder.HasIndex(race => new { race.RacecourseId, race.ScheduledStartUtc })
            .HasDatabaseName("ix_races_racecourse_start");

        builder.Navigation(race => race.Runners)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
