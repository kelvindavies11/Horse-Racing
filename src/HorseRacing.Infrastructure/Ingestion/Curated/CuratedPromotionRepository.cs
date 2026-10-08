using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HorseRacing.Infrastructure.Ingestion.Curated;

public sealed class CuratedPromotionRepository(HorseRacingDbContext dbContext)
    : ICuratedPromotionRepository
{
    public async Task<IReadOnlyList<RawPayloadForPromotion>> GetPendingRawPayloadsAsync(
        int batchSize,
        bool retryFailedPayloads,
        IReadOnlyCollection<string> sourceJobNames,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var staleCutoff = DateTimeOffset.UtcNow.AddMinutes(-30);
        await dbContext.CuratedPromotionRuns
            .Where(run =>
                run.Outcome == CuratedPromotionOutcome.Running
                && run.StartedAtUtc < staleCutoff)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.Outcome, CuratedPromotionOutcome.Cancelled)
                    .SetProperty(run => run.CompletedAtUtc, DateTimeOffset.UtcNow)
                    .SetProperty(run => run.ErrorCode, "orphaned_runner")
                    .SetProperty(
                        run => run.ErrorMessage,
                        "The promoter disappeared before completing this payload."),
                cancellationToken);

        var excludedPayloadIds = dbContext.CuratedPromotionRuns
            .Where(run =>
                run.Outcome == CuratedPromotionOutcome.Succeeded
                || run.Outcome == CuratedPromotionOutcome.Skipped
                || run.Outcome == CuratedPromotionOutcome.Running
                || (!retryFailedPayloads && run.Outcome == CuratedPromotionOutcome.Failed))
            .Select(run => run.RawPayloadId);

        var query = dbContext.RawPayloads
            .AsNoTracking()
            .Include(payload => payload.CollectionRun)
            .Where(payload =>
                payload.CollectionRun.Outcome == RawCollectionOutcome.Succeeded
                && !excludedPayloadIds.Contains(payload.Id));

        if (sourceJobNames.Count > 0)
        {
            var sourceJobNameList = sourceJobNames.ToArray();
            query = query.Where(payload => sourceJobNameList.Contains(payload.CollectionRun.JobName));
        }

        var payloads = await query
            .OrderBy(payload => payload.RetrievedAtUtc)
            .ThenBy(payload => payload.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return payloads
            .Select(payload => new RawPayloadForPromotion(
                payload.Id,
                payload.CollectionRunId,
                payload.CollectionRun.JobName,
                payload.CollectionRun.SourceName,
                new Uri(payload.SourceUrl),
                new Uri(payload.EffectiveUrl),
                payload.RetrievedAtUtc,
                payload.HttpStatusCode,
                payload.MediaType,
                payload.CharacterEncoding,
                payload.Sha256,
                payload.Content))
            .ToList();
    }

    public async Task<bool> TryStartAsync(
        CuratedPromotionStart promotionRun,
        CancellationToken cancellationToken)
    {
        dbContext.CuratedPromotionRuns.Add(CuratedPromotionRun.Create(promotionRun));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_curated_promotion_runs_running_payload"
            })
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<int> FinishAsync(
        CuratedPromotionCompletion completion,
        IReadOnlyCollection<CuratedDomainObjectCandidate> domainObjects,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(1899446294)",
                cancellationToken);

            var run = await dbContext.CuratedPromotionRuns.SingleAsync(
                item => item.Id == completion.PromotionRunId,
                cancellationToken);

            var upsertedRecords = 0;
            var uniqueDomainObjects = domainObjects
                .GroupBy(item => new
                {
                    item.SourceSystem,
                    item.DomainObjectType,
                    item.SourceKey
                })
                .Select(group => group.Last())
                .ToList();

            foreach (var domainObject in uniqueDomainObjects)
            {
                var existing = await dbContext.CuratedDomainObjects.SingleOrDefaultAsync(
                    item =>
                        item.SourceSystem == domainObject.SourceSystem
                        && item.DomainObjectType == domainObject.DomainObjectType
                        && item.SourceKey == domainObject.SourceKey,
                    cancellationToken);

                if (existing is null)
                {
                    dbContext.CuratedDomainObjects.Add(
                        CuratedDomainObject.Create(
                            domainObject,
                            run.RawPayloadId,
                            run.RawCollectionRunId,
                            run.Id));
                }
                else
                {
                    existing.ApplyObservation(
                        domainObject,
                        run.RawPayloadId,
                        run.RawCollectionRunId,
                        run.Id);
                }

                upsertedRecords++;
            }

            run.Finish(completion with
            {
                RecordsUpserted = upsertedRecords,
                ErrorCode = Truncate(completion.ErrorCode, 100),
                ErrorMessage = Truncate(completion.ErrorMessage, 2000)
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return upsertedRecords;
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

    private static string? Truncate(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
