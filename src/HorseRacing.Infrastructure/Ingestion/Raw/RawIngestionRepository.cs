using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Ingestion.Raw;

public sealed class RawIngestionRepository(HorseRacingDbContext dbContext)
    : IRawIngestionRepository
{
    public Task<DateTimeOffset?> GetLatestStartAsync(
        Uri sourceUri,
        CancellationToken cancellationToken) =>
        dbContext.RawCollectionRuns
            .Where(run => run.SourceUrl == sourceUri.AbsoluteUri)
            .OrderByDescending(run => run.StartedAtUtc)
            .Select(run => (DateTimeOffset?)run.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task StartAsync(
        RawCollectionStart collectionRun,
        CancellationToken cancellationToken)
    {
        dbContext.RawCollectionRuns.Add(RawCollectionRun.Create(collectionRun));
        await dbContext.SaveChangesAsync(cancellationToken);
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
