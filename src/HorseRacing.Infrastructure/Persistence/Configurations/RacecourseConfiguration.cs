using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class RacecourseConfiguration : IEntityTypeConfiguration<Racecourse>
{
    public void Configure(EntityTypeBuilder<Racecourse> builder)
    {
        builder.ToTable("racecourses");
        builder.HasKey(racecourse => racecourse.Id).HasName("pk_racecourses");

        builder.Property(racecourse => racecourse.Id).HasColumnName("id");
        builder.Property(racecourse => racecourse.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(racecourse => racecourse.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2)
            .IsRequired();

        builder.HasIndex(racecourse => racecourse.Name)
            .IsUnique()
            .HasDatabaseName("ux_racecourses_name");
    }
}
