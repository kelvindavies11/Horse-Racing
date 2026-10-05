using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class OwnerConfiguration : IEntityTypeConfiguration<Owner>
{
    public void Configure(EntityTypeBuilder<Owner> builder)
    {
        builder.ToTable("owners", table => table.HasCheckConstraint("ck_owners_type", "type IN ('Individual', 'Partnership', 'Syndicate', 'Company', 'RacingClub')"));
        builder.HasKey(owner => owner.Id).HasName("pk_owners");

        builder.Property(owner => owner.Id).HasColumnName("id");
        builder.Property(owner => owner.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(owner => owner.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(owner => owner.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2)
            .IsRequired();

        builder.HasIndex(owner => new { owner.DisplayName, owner.Type, owner.CountryCode })
            .HasDatabaseName("ix_owners_identity");
    }
}
