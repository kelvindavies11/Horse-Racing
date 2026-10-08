using HorseRacing.Application.Ingestion.Raw;

namespace HorseRacing.Infrastructure.Ingestion.Raw;

public sealed class RawCollectionRun
{
    private RawCollectionRun()
    {
    }

    private RawCollectionRun(RawCollectionStart start)
    {
        Id = start.RunId;
        JobName = start.JobName;
        SourceName = start.SourceName;
        SourceUrl = start.SourceUri.AbsoluteUri;
        CollectorVersion = start.CollectorVersion;
        DispatchItemId = start.DispatchItemId;
        StartedAtUtc = start.StartedAtUtc;
        Outcome = RawCollectionOutcome.Running;
    }

    public Guid Id { get; private set; }

    public string JobName { get; private set; } = string.Empty;

    public string SourceName { get; private set; } = string.Empty;

    public string SourceUrl { get; private set; } = string.Empty;

    public string CollectorVersion { get; private set; } = string.Empty;

    public string? DispatchItemId { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public RawCollectionOutcome Outcome { get; private set; }

    public int? HttpStatusCode { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public RawPayload? Payload { get; private set; }

    public static RawCollectionRun Create(RawCollectionStart start) => new(start);

    public void Finish(RawCollectionCompletion completion)
    {
        if (Outcome != RawCollectionOutcome.Running)
        {
            throw new InvalidOperationException("A completed collection run cannot be changed.");
        }

        if (completion.RunId != Id || completion.Outcome == RawCollectionOutcome.Running)
        {
            throw new InvalidOperationException("The collection completion is not valid for this run.");
        }

        CompletedAtUtc = completion.CompletedAtUtc;
        Outcome = completion.Outcome;
        HttpStatusCode = completion.HttpStatusCode;
        ErrorCode = completion.ErrorCode;
        ErrorMessage = completion.ErrorMessage;
    }
}
