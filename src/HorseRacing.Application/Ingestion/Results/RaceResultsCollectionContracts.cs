using HorseRacing.Application.Ingestion.Raw;

namespace HorseRacing.Application.Ingestion.Results;

public interface IRaceResultsRawSourceClient : IRawSourceClient;

public interface IRaceResultsPayloadInterpreter
{
    Uri CreateRacecoursesUri();

    Uri CreateFixturePageUri(int year, int month, int page, bool resultsAvailableOnly = true);

    Uri CreateFixtureRacesUri(ResultFixtureReference fixture);

    Uri CreateFixtureGoingUri(ResultFixtureReference fixture);

    Uri CreateRaceDetailsUri(ResultRaceReference race);

    Uri CreateRaceEntriesUri(ResultRaceReference race);

    Uri CreateRaceResultsUri(ResultRaceReference race);

    ResultFixturePage ReadFixturePage(byte[] content);

    IReadOnlyCollection<ResultRaceReference> ReadRaces(byte[] content);
}

public interface IRaceResultsWorkQueue
{
    Task EnqueueAsync(
        RaceResultsWorkItemDefinition definition,
        CancellationToken cancellationToken);

    Task<RaceResultsWorkItem?> ClaimNextAsync(
        string dispatchItemId,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        RaceResultsWorkCompletion completion,
        CancellationToken cancellationToken);

    Task<RaceResultsWorkQueueSummary> GetSummaryAsync(
        string dispatchItemId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetSuccessfulJobNamesAsync(
        string dispatchItemId,
        CancellationToken cancellationToken);
}

public enum RaceResultsWorkType
{
    FixtureRaces,
    RaceResults,
    FixtureGoing,
    RaceDetails,
    RaceEntries
}

public enum RaceResultsWorkDisposition
{
    Succeeded,
    Unavailable,
    Failed
}

public sealed record RaceResultsWorkItemDefinition(
    string DispatchItemId,
    RaceResultsWorkType WorkType,
    string JobName,
    string SourceName,
    Uri SourceUri,
    int Priority,
    bool RefreshCompletedItem = false);

public sealed record RaceResultsWorkItem(
    Guid Id,
    RaceResultsWorkType WorkType,
    string JobName,
    string SourceName,
    Uri SourceUri);

public sealed record RaceResultsWorkCompletion(
    Guid WorkItemId,
    RaceResultsWorkDisposition Disposition,
    Guid RawCollectionRunId,
    Guid? RawPayloadId,
    int? HttpStatusCode,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset? RetryAtUtc = null);

public sealed record RaceResultsWorkQueueSummary(
    int FixtureItems,
    int ResultItems,
    int RaceDetailItems,
    int PendingItems,
    int RunningItems,
    int SucceededItems,
    int UnavailableItems,
    int FailedItems);

public sealed record ResultFixtureReference(
    int FixtureYear,
    int FixtureId,
    DateOnly FixtureDate,
    string CourseName);

public sealed record ResultRaceReference(
    int RaceYear,
    int RaceId,
    int DivisionSequence,
    string RaceName);

public sealed record ResultFixturePage(
    int CurrentPage,
    int LastPage,
    IReadOnlyCollection<ResultFixtureReference> Fixtures);

public sealed record CollectRaceResultsHistoryCommand(
    DateOnly FromDate,
    DateOnly ToDate,
    string CollectorVersion,
    TimeSpan DelayBetweenRequests,
    bool ReuseSuccessfulPayloads = false,
    string? DispatchItemId = null,
    TimeSpan ThrottleRetryBaseDelay = default,
    int MaximumThrottleRetries = 0,
    TimeSpan TransientRetryBaseDelay = default,
    int MaximumTransientRetries = 0,
    TimeSpan ThrottleFallbackRequestInterval = default,
    bool CollectFutureDetails = false);

public sealed record CollectRaceResultsHistoryResult(
    int FixturesFound,
    int RacesFound,
    int ResultPayloadsCollected,
    int PayloadsReused,
    int UnavailableResultPayloads,
    int ThrottleRetries,
    int TransientRetries,
    int FailedCollections,
    IReadOnlyCollection<string> SuccessfulJobNames);
