using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Raw;

namespace HorseRacing.Infrastructure.Ingestion.Curated;

public sealed class CuratedPromotionRun
{
    private CuratedPromotionRun()
    {
    }

    private CuratedPromotionRun(CuratedPromotionStart start)
    {
        Id = start.PromotionRunId;
        JobName = start.JobName;
        PromoterVersion = start.PromoterVersion;
        RawPayloadId = start.RawPayloadId;
        RawCollectionRunId = start.RawCollectionRunId;
        SourceJobName = start.SourceJobName;
        SourceName = start.SourceName;
        SourceUrl = start.SourceUri.AbsoluteUri;
        PayloadSha256 = start.PayloadSha256;
        StartedAtUtc = start.StartedAtUtc;
        Outcome = CuratedPromotionOutcome.Running;
    }

    public Guid Id { get; private set; }

    public string JobName { get; private set; } = string.Empty;

    public string PromoterVersion { get; private set; } = string.Empty;

    public Guid RawPayloadId { get; private set; }

    public Guid RawCollectionRunId { get; private set; }

    public string SourceJobName { get; private set; } = string.Empty;

    public string SourceName { get; private set; } = string.Empty;

    public string SourceUrl { get; private set; } = string.Empty;

    public string PayloadSha256 { get; private set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public CuratedPromotionOutcome Outcome { get; private set; }

    public int RecordsFound { get; private set; }

    public int RecordsUpserted { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public RawPayload RawPayload { get; private set; } = null!;

    public RawCollectionRun RawCollectionRun { get; private set; } = null!;

    public static CuratedPromotionRun Create(CuratedPromotionStart start) => new(start);

    public void Finish(CuratedPromotionCompletion completion)
    {
        if (Outcome != CuratedPromotionOutcome.Running)
        {
            throw new InvalidOperationException("A completed promotion run cannot be changed.");
        }

        if (completion.PromotionRunId != Id
            || completion.Outcome == CuratedPromotionOutcome.Running
            || completion.RecordsFound < 0
            || completion.RecordsUpserted < 0)
        {
            throw new InvalidOperationException("The promotion completion is not valid for this run.");
        }

        CompletedAtUtc = completion.CompletedAtUtc;
        Outcome = completion.Outcome;
        RecordsFound = completion.RecordsFound;
        RecordsUpserted = completion.RecordsUpserted;
        ErrorCode = completion.ErrorCode;
        ErrorMessage = completion.ErrorMessage;
    }
}
