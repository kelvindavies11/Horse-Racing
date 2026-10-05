using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class StableConfiguration : IEntityTypeConfiguration<Stable>
{
    public void Configure(EntityTypeBuilder<Stable> builder)
    {
        builder.ToTable("stables");
        builder.HasKey(stable => stable.Id).HasName("pk_stables");

        builder.Property(stable => stable.Id).HasColumnName("id");
        builder.Property(stable => stable.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(stable => stable.Town)
            .HasColumnName("town")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(stable => stable.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2)
            .IsRequired();

        builder.HasIndex(stable => new { stable.Name, stable.Town, stable.CountryCode })
            .HasDatabaseName("ix_stables_identity");
    }
}
