using HorseRacing.Application.Ingestion.Results;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Ingestion.Raw;

public sealed class RaceResultsWorkQueueRepository(
    HorseRacingDbContext dbContext,
    TimeProvider timeProvider) : IRaceResultsWorkQueue
{
    private static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(5);

    public async Task EnqueueAsync(
        RaceResultsWorkItemDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.DispatchItemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.JobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.SourceName);

        var sourceUrl = definition.SourceUri.AbsoluteUri;
        var existing = await dbContext.RaceResultsWorkQueueItems.SingleOrDefaultAsync(
            item => item.DispatchItemId == definition.DispatchItemId
                && item.JobName == definition.JobName
                && item.SourceUrl == sourceUrl,
            cancellationToken);
        if (existing is not null)
        {
            if (definition.RefreshCompletedItem)
            {
                existing.RefreshIfCompleted(timeProvider.GetUtcNow());
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        dbContext.RaceResultsWorkQueueItems.Add(
            RaceResultsWorkQueueItem.Create(definition, timeProvider.GetUtcNow()));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (!await dbContext.RaceResultsWorkQueueItems.AnyAsync(
                    item => item.DispatchItemId == definition.DispatchItemId
                        && item.JobName == definition.JobName
                        && item.SourceUrl == sourceUrl,
                    cancellationToken))
            {
                throw;
            }
        }
    }

    public async Task<RaceResultsWorkItem?> ClaimNextAsync(
        string dispatchItemId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var staleBefore = now.Subtract(StaleClaimTimeout);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var staleItems = await dbContext.RaceResultsWorkQueueItems
            .Where(item => item.DispatchItemId == dispatchItemId
                && item.Status == RaceResultsWorkQueueStatus.Running
                && item.UpdatedAtUtc <= staleBefore)
            .ToListAsync(cancellationToken);
        foreach (var staleItem in staleItems)
        {
            staleItem.RecoverIfStale(now, StaleClaimTimeout);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var candidates = await dbContext.RaceResultsWorkQueueItems
            .FromSqlInterpolated($$"""
                SELECT *
                FROM raw.result_work_queue
                WHERE dispatch_item_id = {{dispatchItemId}}
                  AND status IN ('Pending', 'Unavailable', 'Failed')
                  AND available_at_utc <= {{now}}
                ORDER BY priority, available_at_utc, created_at_utc
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);
        var candidate = candidates.SingleOrDefault();
        if (candidate is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        candidate.Claim(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RaceResultsWorkItem(
            candidate.Id,
            candidate.WorkType,
            candidate.JobName,
            candidate.SourceName,
            new Uri(candidate.SourceUrl));
    }

    public async Task CompleteAsync(
        RaceResultsWorkCompletion completion,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.RaceResultsWorkQueueItems.SingleAsync(
            candidate => candidate.Id == completion.WorkItemId,
            cancellationToken);
        item.Complete(completion, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RaceResultsWorkQueueSummary> GetSummaryAsync(
        string dispatchItemId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.RaceResultsWorkQueueItems
            .AsNoTracking()
            .Where(item => item.DispatchItemId == dispatchItemId)
            .Select(item => new { item.WorkType, item.Status })
            .ToListAsync(cancellationToken);

        return new RaceResultsWorkQueueSummary(
            items.Count(item => item.WorkType == RaceResultsWorkType.FixtureRaces),
            items.Count(item => item.WorkType == RaceResultsWorkType.RaceResults),
            items.Count(item => item.Status == RaceResultsWorkQueueStatus.Pending),
            items.Count(item => item.Status == RaceResultsWorkQueueStatus.Running),
            items.Count(item => item.Status == RaceResultsWorkQueueStatus.Succeeded),
            items.Count(item => item.Status == RaceResultsWorkQueueStatus.Unavailable),
            items.Count(item => item.Status == RaceResultsWorkQueueStatus.Failed));
    }

    public async Task<IReadOnlyCollection<string>> GetSuccessfulJobNamesAsync(
        string dispatchItemId,
        CancellationToken cancellationToken) =>
        await dbContext.RaceResultsWorkQueueItems
            .AsNoTracking()
            .Where(item => item.DispatchItemId == dispatchItemId
                && item.Status == RaceResultsWorkQueueStatus.Succeeded)
            .Select(item => item.JobName)
            .Distinct()
            .ToListAsync(cancellationToken);
}
