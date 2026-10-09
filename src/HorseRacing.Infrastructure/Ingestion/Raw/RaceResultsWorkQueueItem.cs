using HorseRacing.Application.Ingestion.Results;

namespace HorseRacing.Infrastructure.Ingestion.Raw;

public enum RaceResultsWorkQueueStatus
{
    Pending,
    Running,
    Succeeded,
    Unavailable,
    Failed
}

public sealed class RaceResultsWorkQueueItem
{
    private RaceResultsWorkQueueItem()
    {
    }

    private RaceResultsWorkQueueItem(
        RaceResultsWorkItemDefinition definition,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        DispatchItemId = definition.DispatchItemId;
        WorkType = definition.WorkType;
        JobName = definition.JobName;
        SourceName = definition.SourceName;
        SourceUrl = definition.SourceUri.AbsoluteUri;
        Priority = definition.Priority;
        Status = RaceResultsWorkQueueStatus.Pending;
        AvailableAtUtc = now;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }

    public string DispatchItemId { get; private set; } = string.Empty;

    public RaceResultsWorkType WorkType { get; private set; }

    public string JobName { get; private set; } = string.Empty;

    public string SourceName { get; private set; } = string.Empty;

    public string SourceUrl { get; private set; } = string.Empty;

    public int Priority { get; private set; }

    public RaceResultsWorkQueueStatus Status { get; private set; }

    public DateTimeOffset AvailableAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public Guid? LastRawCollectionRunId { get; private set; }

    public Guid? LastRawPayloadId { get; private set; }

    public int? LastHttpStatusCode { get; private set; }

    public string? LastErrorCode { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static RaceResultsWorkQueueItem Create(
        RaceResultsWorkItemDefinition definition,
        DateTimeOffset now) => new(definition, now);

    public void RefreshIfCompleted(DateTimeOffset now)
    {
        if (Status != RaceResultsWorkQueueStatus.Succeeded)
        {
            return;
        }

        Status = RaceResultsWorkQueueStatus.Pending;
        AvailableAtUtc = now;
        UpdatedAtUtc = now;
        CompletedAtUtc = null;
    }

    public void RecoverIfStale(DateTimeOffset now, TimeSpan staleAfter)
    {
        if (Status != RaceResultsWorkQueueStatus.Running
            || UpdatedAtUtc > now.Subtract(staleAfter))
        {
            return;
        }

        Status = RaceResultsWorkQueueStatus.Pending;
        AvailableAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void Claim(DateTimeOffset now)
    {
        if (Status is not (RaceResultsWorkQueueStatus.Pending
            or RaceResultsWorkQueueStatus.Unavailable
            or RaceResultsWorkQueueStatus.Failed)
            || AvailableAtUtc > now)
        {
            throw new InvalidOperationException("The result work item is not available to claim.");
        }

        Status = RaceResultsWorkQueueStatus.Running;
        AttemptCount++;
        UpdatedAtUtc = now;
        CompletedAtUtc = null;
    }

    public void Complete(RaceResultsWorkCompletion completion, DateTimeOffset now)
    {
        if (Status != RaceResultsWorkQueueStatus.Running || completion.WorkItemId != Id)
        {
            throw new InvalidOperationException("The result work completion is not valid for this item.");
        }

        Status = completion.Disposition switch
        {
            RaceResultsWorkDisposition.Succeeded => RaceResultsWorkQueueStatus.Succeeded,
            RaceResultsWorkDisposition.Unavailable => RaceResultsWorkQueueStatus.Unavailable,
            RaceResultsWorkDisposition.Failed => RaceResultsWorkQueueStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(completion))
        };
        LastRawCollectionRunId = completion.RawCollectionRunId;
        LastRawPayloadId = completion.RawPayloadId;
        LastHttpStatusCode = completion.HttpStatusCode;
        LastErrorCode = completion.ErrorCode;
        LastErrorMessage = completion.ErrorMessage;
        AvailableAtUtc = completion.RetryAtUtc ?? now;
        UpdatedAtUtc = now;
        CompletedAtUtc = now;
    }
}
