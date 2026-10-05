using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class HorseConfiguration : IEntityTypeConfiguration<Horse>
{
    public void Configure(EntityTypeBuilder<Horse> builder)
    {
        builder.ToTable("horses");
        builder.HasKey(horse => horse.Id).HasName("pk_horses");

        builder.Property(horse => horse.Id).HasColumnName("id");
        builder.Property(horse => horse.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(horse => horse.FoaledOn)
            .HasColumnName("foaled_on")
            .HasColumnType("date");
        builder.Property(horse => horse.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2)
            .IsRequired();

        builder.HasIndex(horse => new { horse.Name, horse.FoaledOn, horse.CountryCode })
            .HasDatabaseName("ix_horses_identity");
    }
}
