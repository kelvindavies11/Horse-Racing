using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Infrastructure;
using HorseRacing.Infrastructure.Ingestion.Bha;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddBhaRawCollection(builder.Configuration);

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var logger = scope.ServiceProvider
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("BhaRawCollector");
var options = scope.ServiceProvider
    .GetRequiredService<IOptions<BhaCollectionOptions>>()
    .Value;
var handler = scope.ServiceProvider.GetRequiredService<CollectRawSourceHandler>();

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

logger.LogInformation(
    "Starting {JobName}. This collector is restricted to local, non-commercial use " +
    "of the public BHA racecourses page.",
    options.JobName);

RawCollectionResult result;
try
{
    result = await handler.HandleAsync(
        new CollectRawSourceCommand(
            options.JobName,
            options.SourceName,
            new Uri(options.SourceUrl),
            options.CollectorVersion,
            TimeSpan.FromSeconds(options.MinimumRequestIntervalSeconds)),
        shutdown.Token);
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    logger.LogWarning("Collection was cancelled; the audit result was retained.");
    return 2;
}

if (result.Outcome == RawCollectionOutcome.Succeeded)
{
    logger.LogInformation(
        "Collection run {RunId} stored Raw payload {PayloadId} (HTTP {StatusCode}).",
        result.RunId,
        result.PayloadId,
        result.HttpStatusCode);
    return 0;
}

logger.LogError(
    "Collection run {RunId} failed with {ErrorCode}: {ErrorMessage}",
    result.RunId,
    result.ErrorCode,
    result.ErrorMessage);
return 1;
