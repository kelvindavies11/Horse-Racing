using System.Security.Cryptography;
using System.Text;
using HorseRacing.Application.Ingestion.Raw;

namespace HorseRacing.Application.UnitTests;

public sealed class CollectRawSourceHandlerTests
{
    private static readonly Uri SourceUri =
        new("https://www.britishhorseracing.com/racing/racecourses/");

    [Fact]
    public async Task Successful_response_is_stored_with_source_hash_and_result()
    {
        var content = Encoding.UTF8.GetBytes("<html>racecourses</html>");
        var sourceClient = new StubSourceClient(
            new RawSourceResponse(
                SourceUri,
                200,
                "OK",
                "text/html",
                "utf-8",
                "\"source-version\"",
                null,
                content));
        var repository = new RecordingRepository();
        var handler = CreateHandler(sourceClient, repository);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(RawCollectionOutcome.Succeeded, result.Outcome);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.NotNull(result.PayloadId);
        Assert.Null(result.ErrorCode);
        var start = Assert.Single(repository.Starts);
        Assert.Equal(SourceUri, start.SourceUri);
        Assert.Equal("year-2026:2026-01", start.DispatchItemId);
        var finish = Assert.Single(repository.Finishes);
        Assert.Equal(RawCollectionOutcome.Succeeded, finish.Completion.Outcome);
        Assert.Null(finish.Completion.ErrorCode);
        Assert.NotNull(finish.Payload);
        Assert.Equal(SourceUri, finish.Payload.SourceUri);
        Assert.Equal(content, finish.Payload.Content);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            finish.Payload.Sha256);
    }

    [Fact]
    public async Task Http_failure_stores_received_payload_and_error_details()
    {
        var content = Encoding.UTF8.GetBytes("Service unavailable");
        var sourceClient = new StubSourceClient(
            new RawSourceResponse(
                SourceUri,
                503,
                "Service Unavailable",
                "text/plain",
                "utf-8",
                null,
                null,
                content));
        var repository = new RecordingRepository();
        var handler = CreateHandler(sourceClient, repository);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(RawCollectionOutcome.Failed, result.Outcome);
        Assert.Equal("http_503", result.ErrorCode);
        Assert.Equal("Service Unavailable", result.ErrorMessage);
        var finish = Assert.Single(repository.Finishes);
        Assert.Equal(content, Assert.IsType<RawPayloadCapture>(finish.Payload).Content);
        Assert.Equal("http_503", finish.Completion.ErrorCode);
    }

    [Fact]
    public async Task Request_failure_stores_audit_error_without_a_payload()
    {
        var repository = new RecordingRepository();
        var handler = CreateHandler(
            new StubSourceClient(new HttpRequestException("DNS unavailable")),
            repository);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(RawCollectionOutcome.Failed, result.Outcome);
        Assert.Null(result.PayloadId);
        Assert.Equal("source_request_failed", result.ErrorCode);
        Assert.Equal("DNS unavailable", result.ErrorMessage);
        var finish = Assert.Single(repository.Finishes);
        Assert.Null(finish.Payload);
        Assert.Equal("source_request_failed", finish.Completion.ErrorCode);
        Assert.Equal("DNS unavailable", finish.Completion.ErrorMessage);
    }

    [Fact]
    public async Task Timeout_stores_a_distinct_audit_error_without_a_payload()
    {
        var repository = new RecordingRepository();
        var handler = CreateHandler(
            new StubSourceClient(new TaskCanceledException("The request timed out.")),
            repository);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(RawCollectionOutcome.Failed, result.Outcome);
        Assert.Null(result.PayloadId);
        Assert.Equal("source_timeout", result.ErrorCode);
        Assert.Equal("The request timed out.", result.ErrorMessage);
        var finish = Assert.Single(repository.Finishes);
        Assert.Null(finish.Payload);
        Assert.Equal("source_timeout", finish.Completion.ErrorCode);
    }

    [Fact]
    public async Task Concurrent_collection_is_reused_without_a_second_source_request()
    {
        var existing = new RawCollectionResult(
            Guid.NewGuid(),
            RawCollectionOutcome.Succeeded,
            Guid.NewGuid(),
            200,
            null,
            null,
            Encoding.UTF8.GetBytes("already collected"));
        var repository = new RecordingRepository
        {
            CanStart = false,
            Latest = existing
        };
        var handler = CreateHandler(
            new StubSourceClient(new InvalidOperationException("The source must not be called.")),
            repository);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Same(existing, result);
        Assert.Empty(repository.Starts);
        Assert.Empty(repository.Finishes);
    }

    private static CollectRawSourceHandler CreateHandler(
        IRawSourceClient sourceClient,
        RecordingRepository repository) =>
        new(sourceClient, repository, new FixedTimeProvider());

    private static CollectRawSourceCommand CreateCommand() =>
        new(
            "test-job",
            "BHA racecourses page",
            SourceUri,
            "test",
            TimeSpan.Zero,
            DispatchItemId: "year-2026:2026-01");

    private sealed class StubSourceClient : IRawSourceClient
    {
        private readonly RawSourceResponse? _response;
        private readonly Exception? _exception;

        public StubSourceClient(RawSourceResponse response)
        {
            _response = response;
        }

        public StubSourceClient(Exception exception)
        {
            _exception = exception;
        }

        public Task<RawSourceResponse> GetAsync(
            Uri sourceUri,
            CancellationToken cancellationToken) =>
            _exception is null
                ? Task.FromResult(_response!)
                : Task.FromException<RawSourceResponse>(_exception);
    }

    private sealed class RecordingRepository : IRawIngestionRepository
    {
        public bool CanStart { get; init; } = true;

        public RawCollectionResult? Latest { get; init; }

        public List<RawCollectionStart> Starts { get; } = [];

        public List<(RawCollectionCompletion Completion, RawPayloadCapture? Payload)> Finishes
        {
            get;
        } = [];

        public Task<RawCollectionResult?> GetLatestSuccessfulAsync(
            string jobName,
            Uri sourceUri,
            CancellationToken cancellationToken) =>
            Task.FromResult<RawCollectionResult?>(null);

        public Task<RawCollectionResult?> GetLatestAsync(
            string jobName,
            Uri sourceUri,
            CancellationToken cancellationToken) =>
            Task.FromResult(Latest);

        public Task<bool> TryStartAsync(
            RawCollectionStart collectionRun,
            TimeSpan minimumRequestInterval,
            CancellationToken cancellationToken)
        {
            if (CanStart)
            {
                Starts.Add(collectionRun);
            }

            return Task.FromResult(CanStart);
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

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);
    }
}
