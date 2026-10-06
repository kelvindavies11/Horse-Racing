namespace HorseRacing.Application.Ingestion.Curated;

public interface ICuratedPromotionRepository
{
    Task<IReadOnlyList<RawPayloadForPromotion>> GetPendingRawPayloadsAsync(
        int batchSize,
        bool retryFailedPayloads,
        IReadOnlyCollection<string> sourceJobNames,
        CancellationToken cancellationToken);

    Task StartAsync(
        CuratedPromotionStart promotionRun,
        CancellationToken cancellationToken);

    Task<int> FinishAsync(
        CuratedPromotionCompletion completion,
        IReadOnlyCollection<CuratedDomainObjectCandidate> domainObjects,
        CancellationToken cancellationToken);
}
