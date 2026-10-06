using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Infrastructure;
using HorseRacing.Infrastructure.Ingestion.Curated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

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

logger.LogInformation(
    "Starting BHA Raw-to-Curated promotion for up to {BatchSize} pending Raw payloads.",
    options.BatchSize);

PromoteRawPayloadsResult result;

try
{
    result = await handler.HandleAsync(
        new PromoteRawPayloadsCommand(
            options.JobName,
            options.PromoterVersion,
            options.BatchSize,
            options.RetryFailedPayloads,
            options.SourceJobNames),
        shutdown.Token);
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    logger.LogWarning("Curated promotion was cancelled; completed audit results were retained.");
    return 2;
}

logger.LogInformation(
    "Curated promotion complete: selected {PayloadsSelected}, succeeded {PayloadsSucceeded}, " +
    "skipped {PayloadsSkipped}, failed {PayloadsFailed}, found {RecordsFound}, upserted {RecordsUpserted}.",
    result.PayloadsSelected,
    result.PayloadsSucceeded,
    result.PayloadsSkipped,
    result.PayloadsFailed,
    result.RecordsFound,
    result.RecordsUpserted);

foreach (var payloadResult in result.PayloadResults.Where(item => item.Outcome != CuratedPromotionOutcome.Succeeded))
{
    logger.LogWarning(
        "Promotion run {PromotionRunId} for Raw payload {RawPayloadId} completed as {Outcome}: {ErrorCode} {ErrorMessage}",
        payloadResult.PromotionRunId,
        payloadResult.RawPayloadId,
        payloadResult.Outcome,
        payloadResult.ErrorCode,
        payloadResult.ErrorMessage);
}

return result.PayloadsFailed > 0 ? 1 : 0;
