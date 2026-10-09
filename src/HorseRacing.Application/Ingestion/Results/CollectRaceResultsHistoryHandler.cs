using HorseRacing.Application.Ingestion.Raw;

namespace HorseRacing.Application.Ingestion.Results;

public sealed class CollectRaceResultsHistoryHandler(
    IRaceResultsRawSourceClient sourceClient,
    IRaceResultsPayloadInterpreter interpreter,
    IRawIngestionRepository repository,
    IRaceResultsWorkQueue workQueue,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan UnavailableRetryDelay = TimeSpan.FromHours(24);
    private static readonly TimeSpan FailedRetryDelay = TimeSpan.FromMinutes(15);

    public async Task<CollectRaceResultsHistoryResult> HandleAsync(
        CollectRaceResultsHistoryCommand command,
        CancellationToken cancellationToken)
    {
        Validate(command);

        var collector = new CollectRawSourceHandler(sourceClient, repository, timeProvider);
        var dispatchItemId = command.DispatchItemId
            ?? $"range-{command.FromDate:yyyyMMdd}-{command.ToDate:yyyyMMdd}";
        var successfulJobNames = new HashSet<string>(StringComparer.Ordinal);
        var failedCollections = 0;
        var payloadsReused = 0;
        var throttleRetries = 0;
        var resultPayloadsCollected = 0;

        var racecourses = await CollectAsync(
            collector,
            repository,
            "bha-racecourses-api",
            "BHA racecourse locations API",
            interpreter.CreateRacecoursesUri(),
            command,
            reuseSuccessful: true,
            dispatchItemId,
            cancellationToken);
        Track(racecourses, "bha-racecourses-api");
        if (ShouldStopBatch(racecourses.Result))
        {
            return await BuildResultAsync(0);
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
                    command.ReuseSuccessfulPayloads,
                    dispatchItemId,
                    cancellationToken);
                Track(collection, jobName);

                if (ShouldStopBatch(collection.Result))
                {
                    return await BuildResultAsync(fixtures.Count);
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
                    await workQueue.EnqueueAsync(
                        new RaceResultsWorkItemDefinition(
                            dispatchItemId,
                            RaceResultsWorkType.FixtureRaces,
                            $"bha-results-races-{fixture.FixtureYear:D4}-{fixture.FixtureId}",
                            $"BHA races at {fixture.CourseName}",
                            interpreter.CreateFixtureRacesUri(fixture),
                            Priority: 10,
                            RefreshCompletedItem: !command.ReuseSuccessfulPayloads),
                        cancellationToken);
                }

                page++;
            }
            while (page <= lastPage);
        }

        while (await workQueue.ClaimNextAsync(dispatchItemId, cancellationToken) is { } item)
        {
            var collection = await CollectAsync(
                collector,
                repository,
                item.JobName,
                item.SourceName,
                item.SourceUri,
                command,
                reuseSuccessful: true,
                dispatchItemId,
                cancellationToken);
            throttleRetries += collection.ThrottleRetries;

            if (collection.Result.HttpStatusCode == 404)
            {
                await CompleteAsync(
                    item,
                    collection.Result,
                    RaceResultsWorkDisposition.Unavailable,
                    timeProvider.GetUtcNow().Add(UnavailableRetryDelay));
                continue;
            }

            if (collection.Result.Outcome != RawCollectionOutcome.Succeeded
                || collection.Result.Content is null)
            {
                failedCollections++;
                await CompleteAsync(
                    item,
                    collection.Result,
                    RaceResultsWorkDisposition.Failed,
                    timeProvider.GetUtcNow().Add(FailedRetryDelay));

                if (ShouldStopBatch(collection.Result))
                {
                    break;
                }

                continue;
            }

            successfulJobNames.Add(item.JobName);
            if (collection.Reused)
            {
                payloadsReused++;
            }
            else if (item.WorkType == RaceResultsWorkType.RaceResults)
            {
                resultPayloadsCollected++;
            }

            if (item.WorkType == RaceResultsWorkType.FixtureRaces)
            {
                foreach (var race in interpreter.ReadRaces(collection.Result.Content))
                {
                    await workQueue.EnqueueAsync(
                        new RaceResultsWorkItemDefinition(
                            dispatchItemId,
                            RaceResultsWorkType.RaceResults,
                            $"bha-results-runners-{race.RaceYear:D4}-{race.RaceId}-{race.DivisionSequence}",
                            $"BHA result for {race.RaceName}",
                            interpreter.CreateRaceResultsUri(race),
                            Priority: 0),
                        cancellationToken);
                }
            }

            await CompleteAsync(
                item,
                collection.Result,
                RaceResultsWorkDisposition.Succeeded,
                retryAtUtc: null);
        }

        return await BuildResultAsync(fixtures.Count);

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

            throttleRetries += attempt.ThrottleRetries;
        }

        async Task CompleteAsync(
            RaceResultsWorkItem item,
            RawCollectionResult result,
            RaceResultsWorkDisposition disposition,
            DateTimeOffset? retryAtUtc)
        {
            await workQueue.CompleteAsync(
                new RaceResultsWorkCompletion(
                    item.Id,
                    disposition,
                    result.RunId,
                    result.PayloadId,
                    result.HttpStatusCode,
                    result.ErrorCode,
                    result.ErrorMessage,
                    retryAtUtc),
                cancellationToken);
        }

        async Task<CollectRaceResultsHistoryResult> BuildResultAsync(int fixturesFound)
        {
            var summary = await workQueue.GetSummaryAsync(dispatchItemId, cancellationToken);
            successfulJobNames.UnionWith(
                await workQueue.GetSuccessfulJobNamesAsync(dispatchItemId, cancellationToken));
            return new CollectRaceResultsHistoryResult(
                fixturesFound,
                summary.ResultItems,
                resultPayloadsCollected,
                payloadsReused,
                summary.UnavailableItems,
                throttleRetries,
                failedCollections,
                successfulJobNames.ToList());
        }
    }

    private static async Task<CollectionAttempt> CollectAsync(
        CollectRawSourceHandler collector,
        IRawIngestionRepository repository,
        string jobName,
        string sourceName,
        Uri sourceUri,
        CollectRaceResultsHistoryCommand command,
        bool reuseSuccessful,
        string dispatchItemId,
        CancellationToken cancellationToken)
    {
        if (reuseSuccessful)
        {
            var existing = await repository.GetLatestSuccessfulAsync(
                jobName,
                sourceUri,
                cancellationToken);
            if (existing is not null)
            {
                return new CollectionAttempt(existing, true, 0);
            }
        }

        var throttleRetries = 0;
        while (true)
        {
            var result = await collector.HandleAsync(
                new CollectRawSourceCommand(
                    jobName,
                    sourceName,
                    sourceUri,
                    command.CollectorVersion,
                    command.DelayBetweenRequests,
                    dispatchItemId),
                cancellationToken);

            if (result.HttpStatusCode != 429
                || throttleRetries >= command.MaximumThrottleRetries)
            {
                return new CollectionAttempt(result, false, throttleRetries);
            }

            throttleRetries++;
            var multiplier = 1L << Math.Min(throttleRetries - 1, 2);
            var retryDelayTicks = Math.Min(
                command.ThrottleRetryBaseDelay.Ticks * multiplier,
                TimeSpan.FromHours(1).Ticks);
            await Task.Delay(TimeSpan.FromTicks(retryDelayTicks), cancellationToken);
        }
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

        if (command.ThrottleRetryBaseDelay < TimeSpan.Zero
            || command.ThrottleRetryBaseDelay > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The throttle retry base delay must be between zero and one hour.");
        }

        if (command.MaximumThrottleRetries is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The maximum throttle retry count must be between zero and ten.");
        }
    }

    private sealed record CollectionAttempt(
        RawCollectionResult Result,
        bool Reused,
        int ThrottleRetries);
}
