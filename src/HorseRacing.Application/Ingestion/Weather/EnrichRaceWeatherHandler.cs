using System.Text;
using HorseRacing.Application.Ingestion.Raw;

namespace HorseRacing.Application.Ingestion.Weather;

public sealed class EnrichRaceWeatherHandler(
    IOpenMeteoRawSourceClient sourceClient,
    IOpenMeteoPayloadInterpreter interpreter,
    IRaceWeatherRepository weatherRepository,
    IRawIngestionRepository rawRepository,
    TimeProvider timeProvider)
{
    public async Task<EnrichRaceWeatherResult> HandleAsync(
        EnrichRaceWeatherCommand command,
        CancellationToken cancellationToken)
    {
        Validate(command);
        var targets = await weatherRepository.GetTargetsAsync(
            command.FromDate,
            command.ToDate,
            cancellationToken);
        var collector = new CollectRawSourceHandler(sourceClient, rawRepository, timeProvider);
        var failedCollections = 0;
        var locationsStored = 0;
        var weatherRowsStored = 0;

        var locations = new Dictionary<(string System, string Key), StoredRacecourseLocation>();
        foreach (var courseGroup in targets.GroupBy(target =>
                     (target.SourceSystem, target.SourceCourseKey)))
        {
            var sample = courseGroup.First();
            var stored = await weatherRepository.GetLocationAsync(
                sample.SourceSystem,
                sample.SourceCourseKey,
                cancellationToken);
            if (stored is null)
            {
                stored = await ResolveLocationAsync(sample);
                if (stored is null)
                {
                    continue;
                }

                locationsStored++;
            }

            locations[courseGroup.Key] = stored;
        }

        foreach (var group in targets
                     .Where(target => locations.ContainsKey((target.SourceSystem, target.SourceCourseKey)))
                     .GroupBy(target => new
                     {
                         target.SourceSystem,
                         target.SourceCourseKey,
                         target.LocalRaceDate
                     }))
        {
            var location = locations[(group.Key.SourceSystem, group.Key.SourceCourseKey)];
            var sourceUri = interpreter.CreateHistoricalWeatherUri(
                location.Latitude,
                location.Longitude,
                group.Key.LocalRaceDate,
                location.TimeZone);
            var collection = await CollectAsync(
                collector,
                $"open-meteo-weather-{SafeToken(location.SourceCourseKey)}-{group.Key.LocalRaceDate:yyyyMMdd}",
                $"Open-Meteo weather at {location.CourseName}",
                sourceUri,
                command,
                cancellationToken);
            if (collection.Outcome != RawCollectionOutcome.Succeeded
                || collection.Content is null
                || collection.PayloadId is null)
            {
                failedCollections++;
                continue;
            }

            foreach (var target in group)
            {
                var weather = interpreter.ReadWeather(
                    collection.Content,
                    target.RaceStartUtc,
                    location.TimeZone);
                if (weather is null)
                {
                    failedCollections++;
                    continue;
                }

                await weatherRepository.UpsertWeatherAsync(
                    new RaceWeatherCapture(
                        target.CuratedRaceId,
                        location.Id,
                        target.RaceStartUtc,
                        weather,
                        sourceUri,
                        collection.PayloadId.Value,
                        collection.RunId,
                        timeProvider.GetUtcNow()),
                    cancellationToken);
                weatherRowsStored++;
            }
        }

        return new EnrichRaceWeatherResult(
            targets.Count,
            locationsStored,
            weatherRowsStored,
            failedCollections);

        async Task<StoredRacecourseLocation?> ResolveLocationAsync(RaceWeatherTarget target)
        {
            if (target.Latitude is not null && target.Longitude is not null)
            {
                return await weatherRepository.UpsertLocationAsync(
                    new RacecourseLocationCapture(
                        target.SourceSystem,
                        target.SourceCourseKey,
                        target.CourseName,
                        target.Postcode,
                        target.Latitude.Value,
                        target.Longitude.Value,
                        "Europe/London",
                        "BHA",
                        new Uri(target.LocationSourceUrl),
                        target.LocationRawPayloadId,
                        target.LocationRawCollectionRunId,
                        timeProvider.GetUtcNow()),
                    cancellationToken);
            }

            var sourceUri = interpreter.CreateGeocodingUri(target.CourseName, target.Postcode);
            var collection = await CollectAsync(
                collector,
                $"open-meteo-geocode-{SafeToken(target.SourceCourseKey)}",
                $"Open-Meteo location for {target.CourseName}",
                sourceUri,
                command,
                cancellationToken);
            if (collection.Outcome != RawCollectionOutcome.Succeeded
                || collection.Content is null
                || collection.PayloadId is null)
            {
                failedCollections++;
                return null;
            }

            var resolved = interpreter.ReadLocation(collection.Content);
            if (resolved is null)
            {
                failedCollections++;
                return null;
            }

            return await weatherRepository.UpsertLocationAsync(
                new RacecourseLocationCapture(
                    target.SourceSystem,
                    target.SourceCourseKey,
                    target.CourseName,
                    resolved.Postcode ?? target.Postcode,
                    resolved.Latitude,
                    resolved.Longitude,
                    resolved.TimeZone,
                    "Open-Meteo / GeoNames",
                    sourceUri,
                    collection.PayloadId.Value,
                    collection.RunId,
                    timeProvider.GetUtcNow()),
                cancellationToken);
        }
    }

    private static async Task<RawCollectionResult> CollectAsync(
        CollectRawSourceHandler collector,
        string jobName,
        string sourceName,
        Uri sourceUri,
        EnrichRaceWeatherCommand command,
        CancellationToken cancellationToken)
    {
        var result = await collector.HandleAsync(
            new CollectRawSourceCommand(
                jobName,
                sourceName,
                sourceUri,
                command.CollectorVersion,
                TimeSpan.Zero),
            cancellationToken);
        if (command.DelayBetweenRequests > TimeSpan.Zero)
        {
            await Task.Delay(command.DelayBetweenRequests, cancellationToken);
        }

        return result;
    }

    private static string SafeToken(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-') switch
        {
            "" => "course",
            var token when token.Length > 80 => token[..80],
            var token => token
        };
    }

    private static void Validate(EnrichRaceWeatherCommand command)
    {
        if (command.ToDate < command.FromDate)
        {
            throw new ArgumentException("The weather range end date cannot precede its start date.", nameof(command));
        }

        if (command.ToDate.DayNumber - command.FromDate.DayNumber > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "A weather enrichment run is limited to 32 days.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(command.CollectorVersion);
        if (command.DelayBetweenRequests < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "The request delay cannot be negative.");
        }
    }
}
