using System.Text;
using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Application.Ingestion.Results;

namespace HorseRacing.Application.UnitTests;

public sealed class CollectRaceResultsHistoryHandlerTests
{
    private static readonly DateOnly FromDate = new(2023, 8, 1);
    private static readonly DateOnly ToDate = new(2023, 8, 31);

    [Fact]
    public async Task Reuses_successful_payloads_without_repeating_network_requests()
    {
        var interpreter = new StubInterpreter();
        var repository = new RecordingRepository();
        repository.AddSuccessful("bha-racecourses-api", interpreter.CreateRacecoursesUri());
        repository.AddSuccessful("bha-results-fixtures-2023-08-p1", interpreter.CreateFixturePageUri(2023, 8, 1));
        repository.AddSuccessful(
            "bha-results-races-2023-101",
            interpreter.CreateFixtureRacesUri(StubInterpreter.Fixture));
        repository.AddSuccessful(
            "bha-results-runners-2023-201-0",
            interpreter.CreateRaceResultsUri(StubInterpreter.Races[0]));
        var client = new RecordingSourceClient(_ => Success());
        var handler = CreateHandler(client, interpreter, repository);

        var result = await handler.HandleAsync(CreateCommand(reuse: true), CancellationToken.None);

        Assert.Equal(1, result.FixturesFound);
        Assert.Equal(1, result.RacesFound);
        Assert.Equal(0, result.ResultPayloadsCollected);
        Assert.Equal(4, result.PayloadsReused);
        Assert.Equal(0, result.UnavailableResultPayloads);
        Assert.Equal(0, result.ThrottleRetries);
        Assert.Equal(0, result.FailedCollections);
        Assert.Empty(client.Requests);
        Assert.Empty(repository.Starts);
    }

    [Fact]
    public async Task Missing_placeholder_result_is_audited_but_does_not_fail_the_batch()
    {
        var interpreter = new StubInterpreter();
        var repository = new RecordingRepository();
        var client = new RecordingSourceClient(uri =>
            uri.AbsolutePath.EndsWith("/results", StringComparison.Ordinal)
                ? Response(uri, 404, "Not Found")
                : Success(uri));
        var handler = CreateHandler(client, interpreter, repository);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(0, result.ResultPayloadsCollected);
        Assert.Equal(0, result.PayloadsReused);
        Assert.Equal(1, result.UnavailableResultPayloads);
        Assert.Equal(0, result.ThrottleRetries);
        Assert.Equal(0, result.FailedCollections);
        Assert.Equal(4, client.Requests.Count);
        Assert.All(repository.Starts, start => Assert.Equal("year-2023:2023-08", start.DispatchItemId));
        Assert.Contains(repository.Finishes, item =>
            item.Completion.Outcome == RawCollectionOutcome.Failed
            && item.Completion.HttpStatusCode == 404);
    }

    [Fact]
    public async Task Stops_the_raw_batch_after_terminal_throttling()
    {
        var interpreter = new StubInterpreter(twoRaces: true);
        var repository = new RecordingRepository();
        var client = new RecordingSourceClient(uri =>
            uri.AbsolutePath.EndsWith("/races/201/results", StringComparison.Ordinal)
                ? Response(uri, 429, "Too Many Requests")
                : Success(uri));
        var handler = CreateHandler(client, interpreter, repository);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(1, result.FailedCollections);
        Assert.DoesNotContain(
            client.Requests,
            uri => uri.AbsolutePath.EndsWith("/races/202/results", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Retries_an_audited_throttle_and_continues_the_batch()
    {
        var interpreter = new StubInterpreter(twoRaces: true);
        var repository = new RecordingRepository();
        var firstRaceAttempts = 0;
        var client = new RecordingSourceClient(uri =>
        {
            if (uri.AbsolutePath.EndsWith("/races/201/results", StringComparison.Ordinal)
                && firstRaceAttempts++ == 0)
            {
                return Response(uri, 429, "Too Many Requests");
            }

            return Success(uri);
        });
        var handler = CreateHandler(client, interpreter, repository);

        var command = CreateCommand() with
        {
            ThrottleRetryBaseDelay = TimeSpan.Zero,
            MaximumThrottleRetries = 1
        };
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(2, result.ResultPayloadsCollected);
        Assert.Equal(1, result.ThrottleRetries);
        Assert.Equal(0, result.FailedCollections);
        Assert.Equal(6, client.Requests.Count);
        Assert.Contains(repository.Finishes, item =>
            item.Completion.Outcome == RawCollectionOutcome.Failed
            && item.Completion.HttpStatusCode == 429);
        Assert.Contains(
            client.Requests,
            uri => uri.AbsolutePath.EndsWith("/races/202/results", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Refreshes_partial_month_but_reuses_completed_child_payloads()
    {
        var interpreter = new StubInterpreter();
        var repository = new RecordingRepository();
        repository.AddSuccessful("bha-racecourses-api", interpreter.CreateRacecoursesUri());
        repository.AddSuccessful("bha-results-fixtures-2023-08-p1", interpreter.CreateFixturePageUri(2023, 8, 1));
        repository.AddSuccessful(
            "bha-results-races-2023-101",
            interpreter.CreateFixtureRacesUri(StubInterpreter.Fixture));
        repository.AddSuccessful(
            "bha-results-runners-2023-201-0",
            interpreter.CreateRaceResultsUri(StubInterpreter.Races[0]));
        var client = new RecordingSourceClient(_ => Success());
        var handler = CreateHandler(client, interpreter, repository);

        var result = await handler.HandleAsync(CreateCommand(reuse: false), CancellationToken.None);

        Assert.Single(client.Requests);
        Assert.Contains("/fixtures/2023/8/1", client.Requests[0].AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(3, result.PayloadsReused);
        Assert.Equal(1, result.RacesFound);
        Assert.Equal(0, result.ResultPayloadsCollected);
    }

    [Fact]
    public async Task Caches_unavailable_result_until_its_retry_time()
    {
        var interpreter = new StubInterpreter();
        var repository = new RecordingRepository();
        repository.AddSuccessful("bha-racecourses-api", interpreter.CreateRacecoursesUri());
        repository.AddSuccessful("bha-results-fixtures-2023-08-p1", interpreter.CreateFixturePageUri(2023, 8, 1));
        repository.AddSuccessful(
            "bha-results-races-2023-101",
            interpreter.CreateFixtureRacesUri(StubInterpreter.Fixture));
        var client = new RecordingSourceClient(uri => Response(uri, 404, "Not Found"));
        var queue = new RecordingWorkQueue();
        var handler = CreateHandler(client, interpreter, repository, queue);

        var first = await handler.HandleAsync(CreateCommand(reuse: true), CancellationToken.None);
        var second = await handler.HandleAsync(CreateCommand(reuse: true), CancellationToken.None);

        Assert.Equal(1, first.UnavailableResultPayloads);
        Assert.Equal(1, second.UnavailableResultPayloads);
        Assert.Single(client.Requests);
    }

    private static CollectRaceResultsHistoryHandler CreateHandler(
        RecordingSourceClient client,
        StubInterpreter interpreter,
        RecordingRepository repository,
        RecordingWorkQueue? queue = null) =>
        new(client, interpreter, repository, queue ?? new RecordingWorkQueue(), TimeProvider.System);

    private static CollectRaceResultsHistoryCommand CreateCommand(bool reuse = false) =>
        new(
            FromDate,
            ToDate,
            "2.0.0",
            TimeSpan.Zero,
            reuse,
            DispatchItemId: "year-2023:2023-08");

    private static RawSourceResponse Success(Uri? uri = null) =>
        Response(uri ?? new Uri("https://example.test/success"), 200, "OK");

    private static RawSourceResponse Response(Uri uri, int statusCode, string reasonPhrase) =>
        new(
            uri,
            statusCode,
            reasonPhrase,
            "application/json",
            "utf-8",
            null,
            null,
            Encoding.UTF8.GetBytes("{}"));

    private sealed class RecordingSourceClient(Func<Uri, RawSourceResponse> responseFactory)
        : IRaceResultsRawSourceClient
    {
        public List<Uri> Requests { get; } = [];

        public Task<RawSourceResponse> GetAsync(Uri sourceUri, CancellationToken cancellationToken)
        {
            Requests.Add(sourceUri);
            return Task.FromResult(responseFactory(sourceUri));
        }
    }

    private sealed class StubInterpreter(bool twoRaces = false) : IRaceResultsPayloadInterpreter
    {
        public static ResultFixtureReference Fixture { get; } =
            new(2023, 101, new DateOnly(2023, 8, 12), "York");

        public static IReadOnlyList<ResultRaceReference> Races { get; } =
        [
            new(2023, 201, 0, "First race"),
            new(2023, 202, 0, "Second race")
        ];

        public Uri CreateRacecoursesUri() => new("https://example.test/racecourses");

        public Uri CreateFixturePageUri(int year, int month, int page) =>
            new($"https://example.test/fixtures/{year}/{month}/{page}");

        public Uri CreateFixtureRacesUri(ResultFixtureReference fixture) =>
            new($"https://example.test/fixtures/{fixture.FixtureId}/races");

        public Uri CreateRaceResultsUri(ResultRaceReference race) =>
            new($"https://example.test/races/{race.RaceId}/results");

        public ResultFixturePage ReadFixturePage(byte[] content) =>
            new(1, 1, [Fixture]);

        public IReadOnlyCollection<ResultRaceReference> ReadRaces(byte[] content) =>
            twoRaces ? Races : [Races[0]];
    }

    private sealed class RecordingRepository : IRawIngestionRepository
    {
        private readonly Dictionary<(string JobName, string SourceUrl), RawCollectionResult> successful = [];

        public List<RawCollectionStart> Starts { get; } = [];

        public List<(RawCollectionCompletion Completion, RawPayloadCapture? Payload)> Finishes { get; } = [];

        public void AddSuccessful(string jobName, Uri sourceUri)
        {
            successful[(jobName, sourceUri.AbsoluteUri)] = new RawCollectionResult(
                Guid.NewGuid(),
                RawCollectionOutcome.Succeeded,
                Guid.NewGuid(),
                200,
                null,
                null,
                Encoding.UTF8.GetBytes("{}"));
        }

        public Task<RawCollectionResult?> GetLatestSuccessfulAsync(
            string jobName,
            Uri sourceUri,
            CancellationToken cancellationToken) =>
            Task.FromResult(successful.GetValueOrDefault((jobName, sourceUri.AbsoluteUri)));

        public Task<RawCollectionResult?> GetLatestAsync(
            string jobName,
            Uri sourceUri,
            CancellationToken cancellationToken) =>
            Task.FromResult<RawCollectionResult?>(null);

        public Task<bool> TryStartAsync(
            RawCollectionStart collectionRun,
            TimeSpan minimumRequestInterval,
            CancellationToken cancellationToken)
        {
            Starts.Add(collectionRun);
            return Task.FromResult(true);
        }

        public Task FinishAsync(
            RawCollectionCompletion completion,
            RawPayloadCapture? payload,
            CancellationToken cancellationToken)
        {
            Finishes.Add((completion, payload));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingWorkQueue : IRaceResultsWorkQueue
    {
        private readonly List<QueueEntry> entries = [];

        public Task EnqueueAsync(
            RaceResultsWorkItemDefinition definition,
            CancellationToken cancellationToken)
        {
            var existing = entries.SingleOrDefault(item =>
                item.Definition.DispatchItemId == definition.DispatchItemId
                && item.Definition.JobName == definition.JobName
                && item.Definition.SourceUri == definition.SourceUri);
            if (existing is null)
            {
                entries.Add(new QueueEntry(Guid.NewGuid(), definition));
            }
            else if (definition.RefreshCompletedItem
                && existing.Disposition == RaceResultsWorkDisposition.Succeeded)
            {
                existing.Disposition = null;
                existing.RetryAtUtc = null;
            }

            return Task.CompletedTask;
        }

        public Task<RaceResultsWorkItem?> ClaimNextAsync(
            string dispatchItemId,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var entry = entries
                .Where(item => item.Definition.DispatchItemId == dispatchItemId
                    && !item.Claimed
                    && (item.Disposition is null
                        || item.Disposition is RaceResultsWorkDisposition.Unavailable
                            or RaceResultsWorkDisposition.Failed)
                    && (item.RetryAtUtc is null || item.RetryAtUtc <= now))
                .OrderBy(item => item.Definition.Priority)
                .FirstOrDefault();
            if (entry is null)
            {
                return Task.FromResult<RaceResultsWorkItem?>(null);
            }

            entry.Claimed = true;
            return Task.FromResult<RaceResultsWorkItem?>(new RaceResultsWorkItem(
                entry.Id,
                entry.Definition.WorkType,
                entry.Definition.JobName,
                entry.Definition.SourceName,
                entry.Definition.SourceUri));
        }

        public Task CompleteAsync(
            RaceResultsWorkCompletion completion,
            CancellationToken cancellationToken)
        {
            var entry = entries.Single(item => item.Id == completion.WorkItemId);
            entry.Claimed = false;
            entry.Disposition = completion.Disposition;
            entry.RetryAtUtc = completion.RetryAtUtc;
            return Task.CompletedTask;
        }

        public Task<RaceResultsWorkQueueSummary> GetSummaryAsync(
            string dispatchItemId,
            CancellationToken cancellationToken)
        {
            var matching = entries.Where(item => item.Definition.DispatchItemId == dispatchItemId).ToList();
            return Task.FromResult(new RaceResultsWorkQueueSummary(
                matching.Count(item => item.Definition.WorkType == RaceResultsWorkType.FixtureRaces),
                matching.Count(item => item.Definition.WorkType == RaceResultsWorkType.RaceResults),
                matching.Count(item => item.Disposition is null && !item.Claimed),
                matching.Count(item => item.Claimed),
                matching.Count(item => item.Disposition == RaceResultsWorkDisposition.Succeeded),
                matching.Count(item => item.Disposition == RaceResultsWorkDisposition.Unavailable),
                matching.Count(item => item.Disposition == RaceResultsWorkDisposition.Failed)));
        }

        public Task<IReadOnlyCollection<string>> GetSuccessfulJobNamesAsync(
            string dispatchItemId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<string>>(entries
                .Where(item => item.Definition.DispatchItemId == dispatchItemId
                    && item.Disposition == RaceResultsWorkDisposition.Succeeded)
                .Select(item => item.Definition.JobName)
                .Distinct(StringComparer.Ordinal)
                .ToList());

        private sealed class QueueEntry(Guid id, RaceResultsWorkItemDefinition definition)
        {
            public Guid Id { get; } = id;

            public RaceResultsWorkItemDefinition Definition { get; } = definition;

            public bool Claimed { get; set; }

            public RaceResultsWorkDisposition? Disposition { get; set; }

            public DateTimeOffset? RetryAtUtc { get; set; }
        }
    }
}
