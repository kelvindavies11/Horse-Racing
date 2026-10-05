using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("meetings", table =>
        {
            table.HasCheckConstraint("ck_meetings_type", "type IN ('Flat', 'Jump', 'Mixed')");
            table.HasCheckConstraint("ck_meetings_status", "status IN ('Scheduled', 'InProgress', 'Completed', 'Abandoned')");
        });
        builder.HasKey(meeting => meeting.Id).HasName("pk_meetings");

        builder.Property(meeting => meeting.Id).HasColumnName("id");
        builder.Property(meeting => meeting.RacecourseId).HasColumnName("racecourse_id");
        builder.Property(meeting => meeting.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(meeting => meeting.ScheduledDate)
            .HasColumnName("scheduled_date")
            .HasColumnType("date");
        builder.Property(meeting => meeting.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(meeting => meeting.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(meeting => meeting.Racecourse)
            .WithMany()
            .HasForeignKey(meeting => meeting.RacecourseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_meetings_racecourses_racecourse_id");

        builder.HasIndex(meeting => new
            {
                meeting.RacecourseId,
                meeting.ScheduledDate,
                meeting.Name
            })
            .HasDatabaseName("ix_meetings_racecourse_date_name");
    }
}
