using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Ingestion.Raw;

public sealed class RawIngestionRepository(
    HorseRacingDbContext dbContext,
    TimeProvider timeProvider)
    : IRawIngestionRepository
{
    public async Task<RawCollectionResult?> GetLatestSuccessfulAsync(
        string jobName,
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentNullException.ThrowIfNull(sourceUri);

        var payload = await dbContext.RawPayloads
            .AsNoTracking()
            .Include(item => item.CollectionRun)
            .Where(item =>
                item.CollectionRun.JobName == jobName
                && item.CollectionRun.SourceUrl == sourceUri.AbsoluteUri
                && item.CollectionRun.Outcome == RawCollectionOutcome.Succeeded)
            .OrderByDescending(item => item.RetrievedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return payload is null
            ? null
            : new RawCollectionResult(
                payload.CollectionRunId,
                RawCollectionOutcome.Succeeded,
                payload.Id,
                payload.HttpStatusCode,
                null,
                null,
                payload.Content);
    }

    public async Task<RawCollectionResult?> GetLatestAsync(
        string jobName,
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.RawCollectionRuns
            .AsNoTracking()
            .Include(item => item.Payload)
            .Where(item =>
                item.JobName == jobName
                && item.SourceUrl == sourceUri.AbsoluteUri)
            .OrderByDescending(run => run.StartedAtUtc)
            .ThenByDescending(run => run.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return run is null
            ? null
            : new RawCollectionResult(
                run.Id,
                run.Outcome,
                run.Payload?.Id,
                run.HttpStatusCode,
                run.ErrorCode,
                run.ErrorMessage,
                run.Payload?.Content);
    }

    public async Task<bool> TryStartAsync(
        RawCollectionStart collectionRun,
        TimeSpan minimumRequestInterval,
        CancellationToken cancellationToken)
    {
        var requestScope = $"raw-http:{collectionRun.SourceUri.Scheme}://{collectionRun.SourceUri.Host}:{collectionRun.SourceUri.Port}";
        var sourcePrefix = $"{collectionRun.SourceUri.Scheme}://{collectionRun.SourceUri.Authority}/";
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({requestScope}))",
                cancellationToken);

            var duplicateRun = await dbContext.RawCollectionRuns.SingleOrDefaultAsync(
                run =>
                    run.JobName == collectionRun.JobName
                    && run.SourceUrl == collectionRun.SourceUri.AbsoluteUri
                    && run.Outcome == RawCollectionOutcome.Running,
                cancellationToken);
            if (duplicateRun is not null)
            {
                var now = timeProvider.GetUtcNow();
                if (now - duplicateRun.StartedAtUtc <= TimeSpan.FromMinutes(5))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return false;
                }

                duplicateRun.Finish(new RawCollectionCompletion(
                    duplicateRun.Id,
                    RawCollectionOutcome.Cancelled,
                    now,
                    null,
                    null,
                    "orphaned_runner",
                    "The worker disappeared before completing this collection."));
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var latestStart = await dbContext.RawCollectionRuns
                .Where(run => run.SourceUrl.StartsWith(sourcePrefix))
                .OrderByDescending(run => run.StartedAtUtc)
                .Select(run => (DateTimeOffset?)run.StartedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (latestStart is not null)
            {
                var remainingDelay = minimumRequestInterval
                    - (timeProvider.GetUtcNow() - latestStart.Value);
                if (remainingDelay > TimeSpan.Zero)
                {
                    await Task.Delay(remainingDelay, timeProvider, cancellationToken);
                }
            }

            var actualStart = collectionRun with { StartedAtUtc = timeProvider.GetUtcNow() };
            dbContext.RawCollectionRuns.Add(RawCollectionRun.Create(actualStart));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            finally
            {
                dbContext.ChangeTracker.Clear();
            }

            throw;
        }
    }

    public async Task FinishAsync(
        RawCollectionCompletion completion,
        RawPayloadCapture? payload,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.RawCollectionRuns.SingleAsync(
            item => item.Id == completion.RunId,
            cancellationToken);

        if (payload is not null)
        {
            if (payload.RunId != completion.RunId || payload.PayloadId != completion.PayloadId)
            {
                throw new InvalidOperationException("Raw payload lineage does not match its run.");
            }

            dbContext.RawPayloads.Add(RawPayload.Create(payload));
        }

        run.Finish(completion with
        {
            ErrorCode = Truncate(completion.ErrorCode, 100),
            ErrorMessage = Truncate(completion.ErrorMessage, 2000)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
