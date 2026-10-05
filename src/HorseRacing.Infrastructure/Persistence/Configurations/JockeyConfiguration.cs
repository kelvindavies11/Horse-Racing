using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class JockeyConfiguration : IEntityTypeConfiguration<Jockey>
{
    public void Configure(EntityTypeBuilder<Jockey> builder)
    {
        builder.ToTable("jockeys");
        builder.HasKey(jockey => jockey.Id).HasName("pk_jockeys");

        builder.Property(jockey => jockey.Id).HasColumnName("id");
        builder.Property(jockey => jockey.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(jockey => jockey.LicenceNumber)
            .HasColumnName("licence_number")
            .HasMaxLength(100);
        builder.Property(jockey => jockey.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2)
            .IsRequired();

        builder.HasIndex(jockey => new { jockey.CountryCode, jockey.LicenceNumber })
            .IsUnique()
            .HasDatabaseName("ux_jockeys_licence_number");
    }
}
