using HorseRacing.Application.Ingestion.Weather;
using HorseRacing.Infrastructure.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Raw;

namespace HorseRacing.Infrastructure.Ingestion.Weather;

public sealed class CuratedRaceWeather
{
    private CuratedRaceWeather()
    {
    }

    private CuratedRaceWeather(RaceWeatherCapture capture)
    {
        Id = Guid.NewGuid();
        CuratedRaceId = capture.CuratedRaceId;
        Apply(capture);
    }

    public Guid Id { get; private set; }
    public Guid CuratedRaceId { get; private set; }
    public Guid RacecourseLocationId { get; private set; }
    public DateTimeOffset RaceStartUtc { get; private set; }
    public DateTimeOffset WeatherHourUtc { get; private set; }
    public decimal TemperatureC { get; private set; }
    public decimal ApparentTemperatureC { get; private set; }
    public int RelativeHumidityPercent { get; private set; }
    public decimal PrecipitationMillimetres { get; private set; }
    public decimal RainMillimetres { get; private set; }
    public int WeatherCode { get; private set; }
    public decimal WindSpeedKilometresPerHour { get; private set; }
    public int WindDirectionDegrees { get; private set; }
    public decimal WindGustKilometresPerHour { get; private set; }
    public string SourceUrl { get; private set; } = string.Empty;
    public Guid RawPayloadId { get; private set; }
    public Guid RawCollectionRunId { get; private set; }
    public DateTimeOffset RetrievedAtUtc { get; private set; }
    public CuratedDomainObject CuratedRace { get; private set; } = null!;
    public CuratedRacecourseLocation RacecourseLocation { get; private set; } = null!;
    public RawPayload RawPayload { get; private set; } = null!;
    public RawCollectionRun RawCollectionRun { get; private set; } = null!;

    public static CuratedRaceWeather Create(RaceWeatherCapture capture) => new(capture);

    public void Apply(RaceWeatherCapture capture)
    {
        if (CuratedRaceId != capture.CuratedRaceId)
        {
            throw new InvalidOperationException("The curated race weather identity cannot be changed.");
        }

        RacecourseLocationId = capture.RacecourseLocationId;
        RaceStartUtc = capture.RaceStartUtc;
        WeatherHourUtc = capture.Weather.WeatherHourUtc;
        TemperatureC = capture.Weather.TemperatureC;
        ApparentTemperatureC = capture.Weather.ApparentTemperatureC;
        RelativeHumidityPercent = capture.Weather.RelativeHumidityPercent;
        PrecipitationMillimetres = capture.Weather.PrecipitationMillimetres;
        RainMillimetres = capture.Weather.RainMillimetres;
        WeatherCode = capture.Weather.WeatherCode;
        WindSpeedKilometresPerHour = capture.Weather.WindSpeedKilometresPerHour;
        WindDirectionDegrees = capture.Weather.WindDirectionDegrees;
        WindGustKilometresPerHour = capture.Weather.WindGustKilometresPerHour;
        SourceUrl = capture.SourceUri.AbsoluteUri;
        RawPayloadId = capture.RawPayloadId;
        RawCollectionRunId = capture.RawCollectionRunId;
        RetrievedAtUtc = capture.RetrievedAtUtc;
    }
}
