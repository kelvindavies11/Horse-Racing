using HorseRacing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HorseRacingDbContext))]
partial class HorseRacingDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.9")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("HorseRacing.Domain.Entities.Horse", builder =>
        {
            builder.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            builder.Property<string>("CountryCode").IsRequired().HasMaxLength(2).HasColumnType("character varying(2)").HasColumnName("country_code");
            builder.Property<DateOnly>("FoaledOn").HasColumnType("date").HasColumnName("foaled_on");
            builder.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)").HasColumnName("name");
            builder.HasKey("Id").HasName("pk_horses");
            builder.HasIndex("Name", "FoaledOn", "CountryCode").HasDatabaseName("ix_horses_identity");
            builder.ToTable("horses");
        });

        modelBuilder.Entity("HorseRacing.Domain.Entities.Racecourse", builder =>
        {
            builder.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            builder.Property<string>("CountryCode").IsRequired().HasMaxLength(2).HasColumnType("character varying(2)").HasColumnName("country_code");
            builder.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)").HasColumnName("name");
            builder.HasKey("Id").HasName("pk_racecourses");
            builder.HasIndex("Name").IsUnique().HasDatabaseName("ux_racecourses_name");
            builder.ToTable("racecourses");
        });

        modelBuilder.Entity("HorseRacing.Domain.Entities.Race", builder =>
        {
            builder.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            builder.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)").HasColumnName("name");
            builder.Property<Guid>("RacecourseId").HasColumnType("uuid").HasColumnName("racecourse_id");
            builder.Property<DateTimeOffset>("ScheduledStartUtc").HasColumnType("timestamp with time zone").HasColumnName("scheduled_start_utc");
            builder.Property<RaceStatus>("Status").HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnType("character varying(20)").HasColumnName("status");
            builder.HasKey("Id").HasName("pk_races");
            builder.HasIndex("RacecourseId", "ScheduledStartUtc").HasDatabaseName("ix_races_racecourse_start");
            builder.ToTable("races");
        });

        modelBuilder.Entity("HorseRacing.Domain.Entities.Runner", builder =>
        {
            builder.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            builder.Property<int>("ClothNumber").HasColumnType("integer").HasColumnName("cloth_number");
            builder.Property<decimal?>("DeclaredOdds").HasPrecision(10, 4).HasColumnType("numeric(10,4)").HasColumnName("declared_odds");
            builder.Property<Guid>("HorseId").HasColumnType("uuid").HasColumnName("horse_id");
            builder.Property<Guid>("RaceId").HasColumnType("uuid").HasColumnName("race_id");
            builder.HasKey("Id").HasName("pk_runners");
            builder.HasIndex("HorseId").HasDatabaseName("ix_runners_horse_id");
            builder.HasIndex("RaceId", "ClothNumber").IsUnique().HasDatabaseName("ux_runners_race_cloth_number");
            builder.HasIndex("RaceId", "HorseId").IsUnique().HasDatabaseName("ux_runners_race_horse");
            builder.ToTable("runners");
        });

        modelBuilder.Entity("HorseRacing.Domain.Entities.Race", builder =>
        {
            builder.HasOne("HorseRacing.Domain.Entities.Racecourse", "Racecourse")
                .WithMany()
                .HasForeignKey("RacecourseId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_races_racecourses_racecourse_id");
            builder.Navigation("Racecourse");
        });

        modelBuilder.Entity("HorseRacing.Domain.Entities.Runner", builder =>
        {
            builder.HasOne("HorseRacing.Domain.Entities.Horse", "Horse")
                .WithMany()
                .HasForeignKey("HorseId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_runners_horses_horse_id");
            builder.HasOne("HorseRacing.Domain.Entities.Race", "Race")
                .WithMany("Runners")
                .HasForeignKey("RaceId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired()
                .HasConstraintName("fk_runners_races_race_id");
            builder.Navigation("Horse");
            builder.Navigation("Race");
        });

        modelBuilder.Entity("HorseRacing.Domain.Entities.Race", builder =>
        {
            builder.Navigation("Runners");
        });
    }
}
