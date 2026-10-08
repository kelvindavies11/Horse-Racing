using System.Text;
using HorseRacing.Application.Ingestion.Curated;

namespace HorseRacing.Application.UnitTests;

public sealed class PromoteRawPayloadsHandlerTests
{
    private static readonly Uri SourceUri =
        new("https://api09.horseracing.software/bha/v1/racecourses/");

    [Fact]
    public async Task Successful_extraction_is_audited_and_upserted()
    {
        var payload = CreatePayload();
        var repository = new RecordingRepository([payload], recordsUpserted: 1);
        var extractor = new StubExtractor(
            CuratedRawPayloadExtraction.Succeeded(
            [
                new CuratedDomainObjectCandidate(
                    "BHA",
                    "Racecourse",
                    "ASC",
                    "Ascot",
                    SourceUri,
                    payload.RetrievedAtUtc,
                    "{\"foundData\":{\"courseName\":\"Ascot\"}}")
            ]));
        var handler = CreateHandler(repository, extractor);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(1, result.PayloadsSelected);
        Assert.Equal(1, result.PayloadsSucceeded);
        Assert.Equal(0, result.PayloadsFailed);
        Assert.Equal(1, result.RecordsFound);
        Assert.Equal(1, result.RecordsUpserted);
        Assert.Single(repository.Starts);
        var finish = Assert.Single(repository.Finishes);
        Assert.Equal(CuratedPromotionOutcome.Succeeded, finish.Completion.Outcome);
        Assert.Single(finish.DomainObjects);
    }

    [Fact]
    public async Task Skipped_extraction_retains_reason_without_domain_objects()
    {
        var repository = new RecordingRepository([CreatePayload()], recordsUpserted: 0);
        var extractor = new StubExtractor(
            CuratedRawPayloadExtraction.Skipped(
                "unsupported_media_type",
                "HTML is not promoted."));
        var handler = CreateHandler(repository, extractor);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(1, result.PayloadsSkipped);
        var finish = Assert.Single(repository.Finishes);
        Assert.Equal(CuratedPromotionOutcome.Skipped, finish.Completion.Outcome);
        Assert.Equal("unsupported_media_type", finish.Completion.ErrorCode);
        Assert.Empty(finish.DomainObjects);
    }

    [Fact]
    public async Task Extractor_exception_is_recorded_as_failed_promotion()
    {
        var repository = new RecordingRepository([CreatePayload()], recordsUpserted: 0);
        var extractor = new ThrowingExtractor(new InvalidOperationException("mapping broke"));
        var handler = CreateHandler(repository, extractor);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(1, result.PayloadsFailed);
        var finish = Assert.Single(repository.Finishes);
        Assert.Equal(CuratedPromotionOutcome.Failed, finish.Completion.Outcome);
        Assert.Equal("promotion_failed", finish.Completion.ErrorCode);
        Assert.Equal("mapping broke", finish.Completion.ErrorMessage);
    }

    [Fact]
    public async Task Payload_claimed_by_another_promoter_is_not_processed_twice()
    {
        var repository = new RecordingRepository(
            [CreatePayload()],
            recordsUpserted: 0,
            canStart: false);
        var handler = CreateHandler(
            repository,
            new ThrowingExtractor(new InvalidOperationException("Extractor must not run.")));

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(1, result.PayloadsSkipped);
        Assert.Empty(repository.Starts);
        Assert.Empty(repository.Finishes);
        Assert.Equal("already_promoting", Assert.Single(result.PayloadResults).ErrorCode);
    }

    private static PromoteRawPayloadsHandler CreateHandler(
        RecordingRepository repository,
        ICuratedRawPayloadExtractor extractor) =>
        new(repository, extractor, new FixedTimeProvider());

    private static PromoteRawPayloadsCommand CreateCommand() =>
        new("bha-raw-to-curated", "test", 10, false, []);

    private static RawPayloadForPromotion CreatePayload() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "bha-racecourses-api",
            "BHA racecourses API",
            SourceUri,
            SourceUri,
            new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero),
            200,
            "application/json",
            "utf-8",
            new string('a', 64),
            Encoding.UTF8.GetBytes("[{\"courseName\":\"Ascot\"}]"));

    private sealed class StubExtractor(CuratedRawPayloadExtraction extraction)
        : ICuratedRawPayloadExtractor
    {
        public CuratedRawPayloadExtraction Extract(RawPayloadForPromotion payload) =>
            extraction;
    }

    private sealed class ThrowingExtractor(Exception exception) : ICuratedRawPayloadExtractor
    {
        public CuratedRawPayloadExtraction Extract(RawPayloadForPromotion payload) =>
            throw exception;
    }

    private sealed class RecordingRepository(
        IReadOnlyList<RawPayloadForPromotion> payloads,
        int recordsUpserted,
        bool canStart = true) : ICuratedPromotionRepository
    {
        public List<CuratedPromotionStart> Starts { get; } = [];

        public List<(
            CuratedPromotionCompletion Completion,
            IReadOnlyCollection<CuratedDomainObjectCandidate> DomainObjects)> Finishes { get; } = [];

        public Task<IReadOnlyList<RawPayloadForPromotion>> GetPendingRawPayloadsAsync(
            int batchSize,
            bool retryFailedPayloads,
            IReadOnlyCollection<string> sourceJobNames,
            CancellationToken cancellationToken) =>
            Task.FromResult(payloads);

        public Task<bool> TryStartAsync(
            CuratedPromotionStart promotionRun,
            CancellationToken cancellationToken)
        {
            if (canStart)
            {
                Starts.Add(promotionRun);
            }

            return Task.FromResult(canStart);
        }

        public Task<int> FinishAsync(
            CuratedPromotionCompletion completion,
            IReadOnlyCollection<CuratedDomainObjectCandidate> domainObjects,
            CancellationToken cancellationToken)
        {
            Finishes.Add((completion, domainObjects));
            return Task.FromResult(recordsUpserted);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 10, 5, 16, 0, 0, TimeSpan.Zero);
    }
}
