using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class RaceConfiguration : IEntityTypeConfiguration<Race>
{
    public void Configure(EntityTypeBuilder<Race> builder)
    {
        builder.ToTable("races", table =>
        {
            table.HasCheckConstraint("ck_races_numbers", "race_number > 0 AND distance_metres > 0");
            table.HasCheckConstraint("ck_races_code", "code IN ('Flat', 'Hurdle', 'Steeplechase', 'NationalHuntFlat')");
            table.HasCheckConstraint("ck_races_surface", "surface IN ('Turf', 'AllWeather')");
            table.HasCheckConstraint("ck_races_status", "status IN ('Scheduled', 'Off', 'Finished', 'Abandoned')");
        });
        builder.HasKey(race => race.Id).HasName("pk_races");

        builder.Property(race => race.Id).HasColumnName("id");
        builder.Property(race => race.MeetingId).HasColumnName("meeting_id");
        builder.Property(race => race.RaceNumber).HasColumnName("race_number");
        builder.Property(race => race.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(race => race.ScheduledStartUtc)
            .HasColumnName("scheduled_start_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(race => race.Code)
            .HasColumnName("code")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(race => race.Surface)
            .HasColumnName("surface")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(race => race.DistanceMetres).HasColumnName("distance_metres");
        builder.Property(race => race.GoingDescription)
            .HasColumnName("going_description")
            .HasMaxLength(100);
        builder.Property(race => race.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(race => race.Meeting)
            .WithMany()
            .HasForeignKey(race => race.MeetingId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_races_meetings_meeting_id");

        builder.HasIndex(race => new { race.MeetingId, race.RaceNumber })
            .IsUnique()
            .HasDatabaseName("ux_races_meeting_race_number");
        builder.HasIndex(race => new { race.MeetingId, race.ScheduledStartUtc })
            .HasDatabaseName("ix_races_meeting_start");

        builder.Navigation(race => race.Runners)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
