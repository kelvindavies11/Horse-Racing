using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Infrastructure;
using HorseRacing.Infrastructure.Ingestion.Curated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.Warning);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddBhaCuratedPromotion(builder.Configuration);

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var logger = scope.ServiceProvider
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("BhaCuratedPromoter");
var options = scope.ServiceProvider
    .GetRequiredService<IOptions<BhaCuratedPromotionOptions>>()
    .Value;
var handler = scope.ServiceProvider.GetRequiredService<PromoteRawPayloadsHandler>();

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

var drain = args.Any(argument => argument.Equals("--drain", StringComparison.OrdinalIgnoreCase));
logger.LogInformation(
    "Starting BHA Raw-to-Curated promotion for {Mode} of up to {BatchSize} pending Raw payloads per batch.",
    drain ? "a complete drain" : "one batch",
    options.BatchSize);

var totalSelected = 0;
var totalSucceeded = 0;
var totalSkipped = 0;
var totalFound = 0;
var totalUpserted = 0;

while (true)
{
    PromoteRawPayloadsResult result;
    try
    {
        result = await handler.HandleAsync(
            new PromoteRawPayloadsCommand(
                options.JobName,
                options.PromoterVersion,
                options.BatchSize,
                drain || options.RetryFailedPayloads,
                options.SourceJobNames),
            shutdown.Token);
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
    {
        logger.LogWarning("Curated promotion was cancelled; completed audit results were retained.");
        return 2;
    }

    logger.LogInformation(
        "Curated promotion batch complete: selected {PayloadsSelected}, succeeded {PayloadsSucceeded}, " +
        "skipped {PayloadsSkipped}, failed {PayloadsFailed}, found {RecordsFound}, upserted {RecordsUpserted}.",
        result.PayloadsSelected,
        result.PayloadsSucceeded,
        result.PayloadsSkipped,
        result.PayloadsFailed,
        result.RecordsFound,
        result.RecordsUpserted);

    foreach (var payloadResult in result.PayloadResults.Where(item => item.Outcome == CuratedPromotionOutcome.Failed))
    {
        logger.LogWarning(
            "Promotion run {PromotionRunId} for Raw payload {RawPayloadId} completed as {Outcome}: {ErrorCode} {ErrorMessage}",
            payloadResult.PromotionRunId,
            payloadResult.RawPayloadId,
            payloadResult.Outcome,
            payloadResult.ErrorCode,
            payloadResult.ErrorMessage);
    }

    totalSelected += result.PayloadsSelected;
    totalSucceeded += result.PayloadsSucceeded;
    totalSkipped += result.PayloadsSkipped;
    totalFound += result.RecordsFound;
    totalUpserted += result.RecordsUpserted;

    if (result.PayloadsFailed > 0)
    {
        return 1;
    }

    if (!drain || result.PayloadsSelected == 0)
    {
        break;
    }
}

logger.LogInformation(
    "Curated promotion finished: selected {PayloadsSelected}, succeeded {PayloadsSucceeded}, " +
    "skipped {PayloadsSkipped}, found {RecordsFound}, upserted {RecordsUpserted}.",
    totalSelected,
    totalSucceeded,
    totalSkipped,
    totalFound,
    totalUpserted);

return 0;
