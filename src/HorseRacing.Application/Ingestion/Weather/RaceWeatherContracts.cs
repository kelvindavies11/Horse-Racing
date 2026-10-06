using HorseRacing.Application.Ingestion.Raw;

namespace HorseRacing.Application.Ingestion.Weather;

public interface IOpenMeteoRawSourceClient : IRawSourceClient;

public interface IOpenMeteoPayloadInterpreter
{
    Uri CreateGeocodingUri(string courseName, string? postcode);

    Uri CreateHistoricalWeatherUri(
        decimal latitude,
        decimal longitude,
        DateOnly localDate,
        string timeZone);

    OpenMeteoLocationResult? ReadLocation(byte[] content);

    OpenMeteoWeatherResult? ReadWeather(
        byte[] content,
        DateTimeOffset raceStartUtc,
        string timeZone);
}

public interface IRaceWeatherRepository
{
    Task<IReadOnlyCollection<RaceWeatherTarget>> GetTargetsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);

    Task<StoredRacecourseLocation?> GetLocationAsync(
        string sourceSystem,
        string sourceCourseKey,
        CancellationToken cancellationToken);

    Task<StoredRacecourseLocation> UpsertLocationAsync(
        RacecourseLocationCapture capture,
        CancellationToken cancellationToken);

    Task UpsertWeatherAsync(
        RaceWeatherCapture capture,
        CancellationToken cancellationToken);
}

public sealed record RaceWeatherTarget(
    Guid CuratedRaceId,
    string RaceSourceKey,
    string RaceName,
    DateTimeOffset RaceStartUtc,
    DateOnly LocalRaceDate,
    string SourceSystem,
    string SourceCourseKey,
    string CourseName,
    string? Postcode,
    decimal? Latitude,
    decimal? Longitude,
    string LocationSourceUrl,
    Guid LocationRawPayloadId,
    Guid LocationRawCollectionRunId);

public sealed record StoredRacecourseLocation(
    Guid Id,
    string SourceSystem,
    string SourceCourseKey,
    string CourseName,
    decimal Latitude,
    decimal Longitude,
    string TimeZone);

public sealed record RacecourseLocationCapture(
    string SourceSystem,
    string SourceCourseKey,
    string CourseName,
    string? Postcode,
    decimal Latitude,
    decimal Longitude,
    string TimeZone,
    string LocationSource,
    Uri SourceUri,
    Guid RawPayloadId,
    Guid RawCollectionRunId,
    DateTimeOffset ResolvedAtUtc);

public sealed record OpenMeteoLocationResult(
    string Name,
    string? Postcode,
    decimal Latitude,
    decimal Longitude,
    string TimeZone);

public sealed record OpenMeteoWeatherResult(
    DateTimeOffset WeatherHourUtc,
    decimal TemperatureC,
    decimal ApparentTemperatureC,
    int RelativeHumidityPercent,
    decimal PrecipitationMillimetres,
    decimal RainMillimetres,
    int WeatherCode,
    decimal WindSpeedKilometresPerHour,
    int WindDirectionDegrees,
    decimal WindGustKilometresPerHour);

public sealed record RaceWeatherCapture(
    Guid CuratedRaceId,
    Guid RacecourseLocationId,
    DateTimeOffset RaceStartUtc,
    OpenMeteoWeatherResult Weather,
    Uri SourceUri,
    Guid RawPayloadId,
    Guid RawCollectionRunId,
    DateTimeOffset RetrievedAtUtc);

public sealed record EnrichRaceWeatherCommand(
    DateOnly FromDate,
    DateOnly ToDate,
    string CollectorVersion,
    TimeSpan DelayBetweenRequests);

public sealed record EnrichRaceWeatherResult(
    int RaceTargets,
    int LocationsStored,
    int WeatherRowsStored,
    int FailedCollections);
