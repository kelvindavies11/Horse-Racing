using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Raw;

namespace HorseRacing.Infrastructure.Ingestion.Curated;

public sealed class CuratedDomainObject
{
    private CuratedDomainObject()
    {
    }

    private CuratedDomainObject(
        CuratedDomainObjectCandidate candidate,
        Guid rawPayloadId,
        Guid rawCollectionRunId,
        Guid promotionRunId)
    {
        Id = Guid.NewGuid();
        SourceSystem = candidate.SourceSystem;
        DomainObjectType = candidate.DomainObjectType;
        SourceKey = candidate.SourceKey;
        FirstObservedAtUtc = candidate.ObservedAtUtc;
        ApplyObservation(candidate, rawPayloadId, rawCollectionRunId, promotionRunId);
    }

    public Guid Id { get; private set; }

    public string SourceSystem { get; private set; } = string.Empty;

    public string DomainObjectType { get; private set; } = string.Empty;

    public string SourceKey { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string SourceUrl { get; private set; } = string.Empty;

    public Guid RawPayloadId { get; private set; }

    public Guid RawCollectionRunId { get; private set; }

    public Guid LastPromotionRunId { get; private set; }

    public string SourceDataJson { get; private set; } = "{}";

    public DateTimeOffset FirstObservedAtUtc { get; private set; }

    public DateTimeOffset LastObservedAtUtc { get; private set; }

    public RawPayload RawPayload { get; private set; } = null!;

    public RawCollectionRun RawCollectionRun { get; private set; } = null!;

    public CuratedPromotionRun LastPromotionRun { get; private set; } = null!;

    public static CuratedDomainObject Create(
        CuratedDomainObjectCandidate candidate,
        Guid rawPayloadId,
        Guid rawCollectionRunId,
        Guid promotionRunId) =>
        new(candidate, rawPayloadId, rawCollectionRunId, promotionRunId);

    public void ApplyObservation(
        CuratedDomainObjectCandidate candidate,
        Guid rawPayloadId,
        Guid rawCollectionRunId,
        Guid promotionRunId)
    {
        if (!string.Equals(SourceSystem, candidate.SourceSystem, StringComparison.Ordinal)
            || !string.Equals(DomainObjectType, candidate.DomainObjectType, StringComparison.Ordinal)
            || !string.Equals(SourceKey, candidate.SourceKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The curated observation identity cannot be changed.");
        }

        if (candidate.ObservedAtUtc < FirstObservedAtUtc)
        {
            FirstObservedAtUtc = candidate.ObservedAtUtc;
        }

        if (LastObservedAtUtc != default
            && candidate.ObservedAtUtc < LastObservedAtUtc)
        {
            return;
        }

        DisplayName = candidate.DisplayName;
        SourceUrl = candidate.SourceUri.AbsoluteUri;
        RawPayloadId = rawPayloadId;
        RawCollectionRunId = rawCollectionRunId;
        LastPromotionRunId = promotionRunId;
        SourceDataJson = candidate.SourceDataJson;
        LastObservedAtUtc = candidate.ObservedAtUtc;
    }
}
