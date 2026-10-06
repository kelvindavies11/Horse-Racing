namespace HorseRacing.Application.Ingestion.Curated;

public sealed class PromoteRawPayloadsHandler(
    ICuratedPromotionRepository repository,
    ICuratedRawPayloadExtractor extractor,
    TimeProvider timeProvider)
{
    public async Task<PromoteRawPayloadsResult> HandleAsync(
        PromoteRawPayloadsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.JobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.PromoterVersion);

        if (command.BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The promotion batch size must be greater than zero.");
        }

        var payloads = await repository.GetPendingRawPayloadsAsync(
            command.BatchSize,
            command.RetryFailedPayloads,
            command.SourceJobNames,
            cancellationToken);

        var results = new List<CuratedPayloadPromotionResult>(payloads.Count);

        foreach (var payload in payloads)
        {
            var promotionRunId = Guid.NewGuid();
            var startedAtUtc = timeProvider.GetUtcNow();

            await repository.StartAsync(
                new CuratedPromotionStart(
                    promotionRunId,
                    command.JobName,
                    command.PromoterVersion,
                    payload.PayloadId,
                    payload.CollectionRunId,
                    payload.JobName,
                    payload.SourceName,
                    payload.SourceUri,
                    payload.Sha256,
                    startedAtUtc),
                cancellationToken);

            CuratedPayloadPromotionResult result;

            try
            {
                var extraction = extractor.Extract(payload);
                var recordsFound = extraction.DomainObjects.Count;
                var recordsUpserted = await repository.FinishAsync(
                    new CuratedPromotionCompletion(
                        promotionRunId,
                        extraction.Outcome,
                        timeProvider.GetUtcNow(),
                        recordsFound,
                        recordsFound,
                        extraction.ErrorCode,
                        extraction.ErrorMessage),
                    extraction.DomainObjects,
                    cancellationToken);

                result = new CuratedPayloadPromotionResult(
                    promotionRunId,
                    payload.PayloadId,
                    extraction.Outcome,
                    recordsFound,
                    recordsUpserted,
                    extraction.ErrorCode,
                    extraction.ErrorMessage);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await FinishCancelledAsync(promotionRunId);
                throw;
            }
            catch (Exception exception)
            {
                await repository.FinishAsync(
                    new CuratedPromotionCompletion(
                        promotionRunId,
                        CuratedPromotionOutcome.Failed,
                        timeProvider.GetUtcNow(),
                        0,
                        0,
                        "promotion_failed",
                        exception.Message),
                    [],
                    CancellationToken.None);

                result = new CuratedPayloadPromotionResult(
                    promotionRunId,
                    payload.PayloadId,
                    CuratedPromotionOutcome.Failed,
                    0,
                    0,
                    "promotion_failed",
                    exception.Message);
            }

            results.Add(result);
        }

        return new PromoteRawPayloadsResult(
            payloads.Count,
            results.Count(result => result.Outcome == CuratedPromotionOutcome.Succeeded),
            results.Count(result => result.Outcome == CuratedPromotionOutcome.Skipped),
            results.Count(result => result.Outcome == CuratedPromotionOutcome.Failed),
            results.Sum(result => result.RecordsFound),
            results.Sum(result => result.RecordsUpserted),
            results);
    }

    private Task FinishCancelledAsync(Guid promotionRunId) =>
        repository.FinishAsync(
            new CuratedPromotionCompletion(
                promotionRunId,
                CuratedPromotionOutcome.Cancelled,
                timeProvider.GetUtcNow(),
                0,
                0,
                "cancelled",
                "Promotion was cancelled."),
            [],
            CancellationToken.None);
}
