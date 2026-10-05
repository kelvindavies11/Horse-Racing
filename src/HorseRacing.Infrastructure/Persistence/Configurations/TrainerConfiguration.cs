using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

internal sealed class TrainerConfiguration : IEntityTypeConfiguration<Trainer>
{
    public void Configure(EntityTypeBuilder<Trainer> builder)
    {
        builder.ToTable("trainers");
        builder.HasKey(trainer => trainer.Id).HasName("pk_trainers");

        builder.Property(trainer => trainer.Id).HasColumnName("id");
        builder.Property(trainer => trainer.StableId).HasColumnName("stable_id");
        builder.Property(trainer => trainer.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(trainer => trainer.LicenceNumber)
            .HasColumnName("licence_number")
            .HasMaxLength(100);
        builder.Property(trainer => trainer.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2)
            .IsRequired();

        builder.HasOne(trainer => trainer.Stable)
            .WithMany()
            .HasForeignKey(trainer => trainer.StableId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_trainers_stables_stable_id");

        builder.HasIndex(trainer => new { trainer.CountryCode, trainer.LicenceNumber })
            .IsUnique()
            .HasDatabaseName("ux_trainers_licence_number");
        builder.HasIndex(trainer => trainer.StableId)
            .HasDatabaseName("ix_trainers_stable_id");
    }
}
