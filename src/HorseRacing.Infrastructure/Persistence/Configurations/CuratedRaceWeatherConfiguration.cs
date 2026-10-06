using HorseRacing.Infrastructure.Ingestion.Weather;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HorseRacing.Infrastructure.Persistence.Configurations;

public sealed class CuratedRaceWeatherConfiguration : IEntityTypeConfiguration<CuratedRaceWeather>
{
    public void Configure(EntityTypeBuilder<CuratedRaceWeather> builder)
    {
        builder.ToTable("race_weather", "curated", table =>
        {
            table.HasCheckConstraint("ck_curated_race_weather_humidity", "relative_humidity_percent BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_curated_race_weather_precipitation", "precipitation_millimetres >= 0 AND rain_millimetres >= 0");
            table.HasCheckConstraint("ck_curated_race_weather_wind", "wind_speed_kilometres_per_hour >= 0 AND wind_gust_kilometres_per_hour >= 0 AND wind_direction_degrees BETWEEN 0 AND 360");
            table.HasCheckConstraint("ck_curated_race_weather_code", "weather_code BETWEEN 0 AND 99");
        });
        builder.HasKey(weather => weather.Id);
        builder.Property(weather => weather.Id).HasColumnName("id");
        builder.Property(weather => weather.CuratedRaceId).HasColumnName("curated_race_id").IsRequired();
        builder.Property(weather => weather.RacecourseLocationId).HasColumnName("racecourse_location_id").IsRequired();
        builder.Property(weather => weather.RaceStartUtc).HasColumnName("race_start_utc").IsRequired();
        builder.Property(weather => weather.WeatherHourUtc).HasColumnName("weather_hour_utc").IsRequired();
        builder.Property(weather => weather.TemperatureC).HasColumnName("temperature_c").HasPrecision(5, 2).IsRequired();
        builder.Property(weather => weather.ApparentTemperatureC).HasColumnName("apparent_temperature_c").HasPrecision(5, 2).IsRequired();
        builder.Property(weather => weather.RelativeHumidityPercent).HasColumnName("relative_humidity_percent").IsRequired();
        builder.Property(weather => weather.PrecipitationMillimetres).HasColumnName("precipitation_millimetres").HasPrecision(8, 2).IsRequired();
        builder.Property(weather => weather.RainMillimetres).HasColumnName("rain_millimetres").HasPrecision(8, 2).IsRequired();
        builder.Property(weather => weather.WeatherCode).HasColumnName("weather_code").IsRequired();
        builder.Property(weather => weather.WindSpeedKilometresPerHour).HasColumnName("wind_speed_kilometres_per_hour").HasPrecision(7, 2).IsRequired();
        builder.Property(weather => weather.WindDirectionDegrees).HasColumnName("wind_direction_degrees").IsRequired();
        builder.Property(weather => weather.WindGustKilometresPerHour).HasColumnName("wind_gust_kilometres_per_hour").HasPrecision(7, 2).IsRequired();
        builder.Property(weather => weather.SourceUrl).HasColumnName("source_url").HasMaxLength(2048).IsRequired();
        builder.Property(weather => weather.RawPayloadId).HasColumnName("raw_payload_id").IsRequired();
        builder.Property(weather => weather.RawCollectionRunId).HasColumnName("raw_collection_run_id").IsRequired();
        builder.Property(weather => weather.RetrievedAtUtc).HasColumnName("retrieved_at_utc").IsRequired();

        builder.HasIndex(weather => weather.CuratedRaceId)
            .IsUnique()
            .HasDatabaseName("ux_curated_race_weather_curated_race_id");
        builder.HasIndex(weather => new { weather.RacecourseLocationId, weather.RaceStartUtc })
            .HasDatabaseName("ix_curated_race_weather_location_start");
        builder.HasIndex(weather => weather.RawPayloadId)
            .HasDatabaseName("ix_curated_race_weather_raw_payload_id");
        builder.HasIndex(weather => weather.RawCollectionRunId)
            .HasDatabaseName("ix_curated_race_weather_raw_collection_run_id");

        builder.HasOne(weather => weather.CuratedRace)
            .WithMany()
            .HasForeignKey(weather => weather.CuratedRaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_race_weather_domain_objects_curated_race_id");
        builder.HasOne(weather => weather.RacecourseLocation)
            .WithMany()
            .HasForeignKey(weather => weather.RacecourseLocationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_race_weather_locations_racecourse_location_id");
        builder.HasOne(weather => weather.RawPayload)
            .WithMany()
            .HasForeignKey(weather => weather.RawPayloadId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_race_weather_raw_payloads_raw_payload_id");
        builder.HasOne(weather => weather.RawCollectionRun)
            .WithMany()
            .HasForeignKey(weather => weather.RawCollectionRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curated_race_weather_raw_collection_runs_raw_collection_run_id");
    }
}
