using HorseRacing.Application.Ingestion.Raw;

namespace HorseRacing.Application.Ingestion.Results;

public sealed class CollectRaceResultsHistoryHandler(
    IRaceResultsRawSourceClient sourceClient,
    IRaceResultsPayloadInterpreter interpreter,
    IRawIngestionRepository repository,
    TimeProvider timeProvider)
{
    public async Task<CollectRaceResultsHistoryResult> HandleAsync(
        CollectRaceResultsHistoryCommand command,
        CancellationToken cancellationToken)
    {
        Validate(command);

        var collector = new CollectRawSourceHandler(sourceClient, repository, timeProvider);
        var successfulJobNames = new List<string>();
        var failedCollections = 0;

        var racecourses = await CollectAsync(
            collector,
            "bha-racecourses-api",
            "BHA racecourse locations API",
            interpreter.CreateRacecoursesUri(),
            command,
            cancellationToken);
        Track(racecourses, "bha-racecourses-api");

        var fixtures = new Dictionary<(int Year, int Id), ResultFixtureReference>();
        foreach (var (year, month) in EnumerateMonths(command.FromDate, command.ToDate))
        {
            var page = 1;
            var lastPage = 1;
            do
            {
                var jobName = $"bha-results-fixtures-{year:D4}-{month:D2}-p{page}";
                var collection = await CollectAsync(
                    collector,
                    jobName,
                    "BHA result fixtures API",
                    interpreter.CreateFixturePageUri(year, month, page),
                    command,
                    cancellationToken);
                Track(collection, jobName);

                if (collection.Outcome != RawCollectionOutcome.Succeeded
                    || collection.Content is null)
                {
                    page++;
                    continue;
                }

                var fixturePage = interpreter.ReadFixturePage(collection.Content);
                lastPage = Math.Max(page, fixturePage.LastPage);
                foreach (var fixture in fixturePage.Fixtures.Where(item =>
                             item.FixtureDate >= command.FromDate
                             && item.FixtureDate <= command.ToDate))
                {
                    fixtures[(fixture.FixtureYear, fixture.FixtureId)] = fixture;
                }

                page++;
            }
            while (page <= lastPage);
        }

        var racesFound = 0;
        var resultPayloadsCollected = 0;
        foreach (var fixture in fixtures.Values
                     .OrderBy(item => item.FixtureDate)
                     .ThenBy(item => item.CourseName))
        {
            var racesJobName = $"bha-results-races-{fixture.FixtureYear:D4}-{fixture.FixtureId}";
            var racesCollection = await CollectAsync(
                collector,
                racesJobName,
                $"BHA races at {fixture.CourseName}",
                interpreter.CreateFixtureRacesUri(fixture),
                command,
                cancellationToken);
            Track(racesCollection, racesJobName);

            if (racesCollection.Outcome != RawCollectionOutcome.Succeeded
                || racesCollection.Content is null)
            {
                continue;
            }

            var races = interpreter.ReadRaces(racesCollection.Content);
            racesFound += races.Count;

            foreach (var race in races)
            {
                var resultsJobName = $"bha-results-runners-{race.RaceYear:D4}-{race.RaceId}-{race.DivisionSequence}";
                var resultsCollection = await CollectAsync(
                    collector,
                    resultsJobName,
                    $"BHA result for {race.RaceName}",
                    interpreter.CreateRaceResultsUri(race),
                    command,
                    cancellationToken);
                Track(resultsCollection, resultsJobName);
                if (resultsCollection.Outcome == RawCollectionOutcome.Succeeded)
                {
                    resultPayloadsCollected++;
                }
            }
        }

        return new CollectRaceResultsHistoryResult(
            fixtures.Count,
            racesFound,
            resultPayloadsCollected,
            failedCollections,
            successfulJobNames.Distinct(StringComparer.Ordinal).ToList());

        void Track(RawCollectionResult result, string jobName)
        {
            if (result.Outcome == RawCollectionOutcome.Succeeded)
            {
                successfulJobNames.Add(jobName);
            }
            else
            {
                failedCollections++;
            }
        }
    }

    private static async Task<RawCollectionResult> CollectAsync(
        CollectRawSourceHandler collector,
        string jobName,
        string sourceName,
        Uri sourceUri,
        CollectRaceResultsHistoryCommand command,
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

    private static IEnumerable<(int Year, int Month)> EnumerateMonths(
        DateOnly fromDate,
        DateOnly toDate)
    {
        var current = new DateOnly(fromDate.Year, fromDate.Month, 1);
        var end = new DateOnly(toDate.Year, toDate.Month, 1);
        while (current <= end)
        {
            yield return (current.Year, current.Month);
            current = current.AddMonths(1);
        }
    }

    private static void Validate(CollectRaceResultsHistoryCommand command)
    {
        if (command.ToDate < command.FromDate)
        {
            throw new ArgumentException("The result range end date cannot precede its start date.", nameof(command));
        }

        if (command.ToDate.DayNumber - command.FromDate.DayNumber > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "A result collection run is limited to 32 days.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(command.CollectorVersion);
        if (command.DelayBetweenRequests < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "The request delay cannot be negative.");
        }
    }
}
