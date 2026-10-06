namespace HorseRacing.Application.Ingestion.Curated;

public sealed record PromoteRawPayloadsCommand(
    string JobName,
    string PromoterVersion,
    int BatchSize,
    bool RetryFailedPayloads,
    IReadOnlyCollection<string> SourceJobNames);

public sealed record RawPayloadForPromotion(
    Guid PayloadId,
    Guid CollectionRunId,
    string JobName,
    string SourceName,
    Uri SourceUri,
    Uri EffectiveUri,
    DateTimeOffset RetrievedAtUtc,
    int HttpStatusCode,
    string? MediaType,
    string? CharacterEncoding,
    string Sha256,
    byte[] Content);

public sealed record CuratedPromotionStart(
    Guid PromotionRunId,
    string JobName,
    string PromoterVersion,
    Guid RawPayloadId,
    Guid RawCollectionRunId,
    string SourceJobName,
    string SourceName,
    Uri SourceUri,
    string PayloadSha256,
    DateTimeOffset StartedAtUtc);

public sealed record CuratedDomainObjectCandidate(
    string SourceSystem,
    string DomainObjectType,
    string SourceKey,
    string DisplayName,
    Uri SourceUri,
    DateTimeOffset ObservedAtUtc,
    string SourceDataJson);

public sealed record CuratedRawPayloadExtraction(
    CuratedPromotionOutcome Outcome,
    IReadOnlyCollection<CuratedDomainObjectCandidate> DomainObjects,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static CuratedRawPayloadExtraction Succeeded(
        IReadOnlyCollection<CuratedDomainObjectCandidate> domainObjects) =>
        new(
            CuratedPromotionOutcome.Succeeded,
            domainObjects,
            null,
            null);

    public static CuratedRawPayloadExtraction Skipped(
        string errorCode,
        string errorMessage) =>
        new(
            CuratedPromotionOutcome.Skipped,
            [],
            errorCode,
            errorMessage);

    public static CuratedRawPayloadExtraction Failed(
        string errorCode,
        string errorMessage) =>
        new(
            CuratedPromotionOutcome.Failed,
            [],
            errorCode,
            errorMessage);
}

public sealed record CuratedPromotionCompletion(
    Guid PromotionRunId,
    CuratedPromotionOutcome Outcome,
    DateTimeOffset CompletedAtUtc,
    int RecordsFound,
    int RecordsUpserted,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record CuratedPayloadPromotionResult(
    Guid PromotionRunId,
    Guid RawPayloadId,
    CuratedPromotionOutcome Outcome,
    int RecordsFound,
    int RecordsUpserted,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record PromoteRawPayloadsResult(
    int PayloadsSelected,
    int PayloadsSucceeded,
    int PayloadsSkipped,
    int PayloadsFailed,
    int RecordsFound,
    int RecordsUpserted,
    IReadOnlyCollection<CuratedPayloadPromotionResult> PayloadResults);
