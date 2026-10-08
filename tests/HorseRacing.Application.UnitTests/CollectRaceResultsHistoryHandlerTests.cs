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
        Assert.Equal(0, result.FailedCollections);
        Assert.Equal(4, client.Requests.Count);
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

    private static CollectRaceResultsHistoryHandler CreateHandler(
        RecordingSourceClient client,
        StubInterpreter interpreter,
        RecordingRepository repository) =>
        new(client, interpreter, repository, TimeProvider.System);

    private static CollectRaceResultsHistoryCommand CreateCommand(bool reuse = false) =>
        new(FromDate, ToDate, "2.0.0", TimeSpan.Zero, reuse);

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
}
