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
        var payloadsReused = 0;
        var unavailableResultPayloads = 0;

        var racecourses = await CollectAsync(
            collector,
            repository,
            "bha-racecourses-api",
            "BHA racecourse locations API",
            interpreter.CreateRacecoursesUri(),
            command,
            cancellationToken);
        Track(racecourses, "bha-racecourses-api");
        if (ShouldStopBatch(racecourses.Result))
        {
            return BuildResult(0, 0, 0);
        }

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
                    repository,
                    jobName,
                    "BHA result fixtures API",
                    interpreter.CreateFixturePageUri(year, month, page),
                    command,
                    cancellationToken);
                Track(collection, jobName);

                if (ShouldStopBatch(collection.Result))
                {
                    return BuildResult(fixtures.Count, 0, 0);
                }

                if (collection.Result.Outcome != RawCollectionOutcome.Succeeded
                    || collection.Result.Content is null)
                {
                    page++;
                    continue;
                }

                var fixturePage = interpreter.ReadFixturePage(collection.Result.Content);
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
                repository,
                racesJobName,
                $"BHA races at {fixture.CourseName}",
                interpreter.CreateFixtureRacesUri(fixture),
                command,
                cancellationToken);
            Track(racesCollection, racesJobName);

            if (ShouldStopBatch(racesCollection.Result))
            {
                return BuildResult(fixtures.Count, racesFound, resultPayloadsCollected);
            }

            if (racesCollection.Result.Outcome != RawCollectionOutcome.Succeeded
                || racesCollection.Result.Content is null)
            {
                continue;
            }

            var races = interpreter.ReadRaces(racesCollection.Result.Content);
            racesFound += races.Count;

            foreach (var race in races)
            {
                var resultsJobName = $"bha-results-runners-{race.RaceYear:D4}-{race.RaceId}-{race.DivisionSequence}";
                var resultsCollection = await CollectAsync(
                    collector,
                    repository,
                    resultsJobName,
                    $"BHA result for {race.RaceName}",
                    interpreter.CreateRaceResultsUri(race),
                    command,
                    cancellationToken);
                if (resultsCollection.Result.HttpStatusCode == 404)
                {
                    unavailableResultPayloads++;
                    continue;
                }

                Track(resultsCollection, resultsJobName);
                if (resultsCollection.Result.Outcome == RawCollectionOutcome.Succeeded
                    && !resultsCollection.Reused)
                {
                    resultPayloadsCollected++;
                }

                if (ShouldStopBatch(resultsCollection.Result))
                {
                    return BuildResult(fixtures.Count, racesFound, resultPayloadsCollected);
                }
            }
        }

        return BuildResult(fixtures.Count, racesFound, resultPayloadsCollected);

        void Track(CollectionAttempt attempt, string jobName)
        {
            var result = attempt.Result;
            if (result.Outcome == RawCollectionOutcome.Succeeded)
            {
                successfulJobNames.Add(jobName);
                if (attempt.Reused)
                {
                    payloadsReused++;
                }
            }
            else
            {
                failedCollections++;
            }
        }

        CollectRaceResultsHistoryResult BuildResult(
            int fixturesFound,
            int totalRacesFound,
            int payloadsCollected) =>
            new(
                fixturesFound,
                totalRacesFound,
                payloadsCollected,
                payloadsReused,
                unavailableResultPayloads,
                failedCollections,
                successfulJobNames.Distinct(StringComparer.Ordinal).ToList());
    }

    private static async Task<CollectionAttempt> CollectAsync(
        CollectRawSourceHandler collector,
        IRawIngestionRepository repository,
        string jobName,
        string sourceName,
        Uri sourceUri,
        CollectRaceResultsHistoryCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ReuseSuccessfulPayloads)
        {
            var existing = await repository.GetLatestSuccessfulAsync(
                jobName,
                sourceUri,
                cancellationToken);
            if (existing is not null)
            {
                return new CollectionAttempt(existing, true);
            }
        }

        var result = await collector.HandleAsync(
            new CollectRawSourceCommand(
                jobName,
                sourceName,
                sourceUri,
                command.CollectorVersion,
                command.DelayBetweenRequests),
            cancellationToken);

        return new CollectionAttempt(result, false);
    }

    private static bool ShouldStopBatch(RawCollectionResult result) =>
        result.Outcome == RawCollectionOutcome.Failed
        && (result.ErrorCode == "source_request_failed"
            || result.HttpStatusCode is 408 or 418 or 429 or >= 500);

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

    private sealed record CollectionAttempt(RawCollectionResult Result, bool Reused);
}
