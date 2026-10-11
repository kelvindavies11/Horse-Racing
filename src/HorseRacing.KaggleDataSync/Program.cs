using System.Globalization;
using System.Text.Json;
using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Infrastructure;
using HorseRacing.Infrastructure.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Kaggle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKaggleRaceformImport(builder.Configuration);
builder.Services.AddBhaCuratedPromotion(builder.Configuration);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("KaggleRaceformDataSync");
var options = host.Services.GetRequiredService<IOptions<KaggleRaceformOptions>>().Value;
var downloader = host.Services.GetRequiredService<KaggleRaceformArchiveDownloader>();
var reader = host.Services.GetRequiredService<KaggleRaceformReader>();

var today = DateOnly.FromDateTime(DateTime.UtcNow);
var fromDate = ReadDateArgument(args, "--from") ?? new DateOnly(2015, 1, 1);
var toDate = ReadDateArgument(args, "--to") ?? today;
if (toDate < fromDate)
{
    logger.LogError("--to must be on or after --from.");
    return 2;
}

var rawOnly = HasSwitch(args, "--raw-only");
var refreshRaw = HasSwitch(args, "--refresh-raw");
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

try
{
    logger.LogInformation(
        "Starting Kaggle Raceform bulk sync from {FromDate} through {ToDate}. Completed monthly Raw chunks will be {ReuseMode}.",
        fromDate,
        toDate,
        refreshRaw ? "replaced" : "reused");
    var archive = await downloader.GetAsync(shutdown.Token);
    await reader.PrepareAsync(archive.DatabasePath, shutdown.Token);

    var captured = 0;
    var reused = 0;
    var rowsRead = 0;
    var month = new DateOnly(fromDate.Year, fromDate.Month, 1);
    while (month <= toDate)
    {
        var monthEnd = month.AddMonths(1).AddDays(-1);
        var chunkFrom = fromDate > month ? fromDate : month;
        var chunkTo = toDate < monthEnd ? toDate : monthEnd;
        var rows = await reader.ReadAsync(
            archive.DatabasePath,
            chunkFrom,
            chunkTo,
            shutdown.Token);
        if (rows.Count == 0)
        {
            logger.LogInformation("No source rows were found for {Month:yyyy-MM}; skipping Raw capture.", month);
            month = month.AddMonths(1);
            continue;
        }

        var batch = new KaggleRaceformRawBatch(
            1,
            options.DatasetSlug,
            options.DatasetVersion,
            options.ArchiveEntry,
            archive.ArchiveSha256,
            chunkFrom,
            chunkTo,
            rows);
        var content = JsonSerializer.SerializeToUtf8Bytes(
            batch,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var sourceUri = new Uri(
            $"https://www.kaggle.com/datasets/{options.DatasetSlug}/versions/{options.DatasetVersion}?rawMonth={month:yyyy-MM}");
        var effectiveUri = new Uri(options.DownloadUrl);

        RawCollectionResult result;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            result = await scope.ServiceProvider
                .GetRequiredService<CaptureRawPayloadHandler>()
                .HandleAsync(
                    new CaptureRawPayloadCommand(
                        options.RawJobName,
                        options.SourceName,
                        sourceUri,
                        effectiveUri,
                        options.CollectorVersion,
                        "application/json",
                        "utf-8",
                        content,
                        $"kaggle-raceform:{month:yyyy-MM}",
                        !refreshRaw),
                    shutdown.Token);
        }

        if (result.Outcome != RawCollectionOutcome.Succeeded)
        {
            logger.LogError(
                "Raw capture for {Month:yyyy-MM} failed: {ErrorCode} {ErrorMessage}",
                month,
                result.ErrorCode,
                result.ErrorMessage);
            return 1;
        }

        if (result.Content is null)
        {
            reused++;
        }
        else
        {
            captured++;
        }
        rowsRead += rows.Count;
        logger.LogInformation(
            "Raw month {Month:yyyy-MM} is ready with {Rows} runner rows in payload {PayloadId}.",
            month,
            rows.Count,
            result.PayloadId);
        month = month.AddMonths(1);
    }

    logger.LogInformation(
        "Raw bulk stage complete: {Rows} rows, {Captured} monthly payloads captured, {Reused} reused.",
        rowsRead,
        captured,
        reused);

    if (rawOnly)
    {
        return 0;
    }

    var totalSelected = 0;
    var totalUpserted = 0;
    while (true)
    {
        PromoteRawPayloadsResult result;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            result = await scope.ServiceProvider
                .GetRequiredService<PromoteRawPayloadsHandler>()
                .HandleAsync(
                    new PromoteRawPayloadsCommand(
                        options.PromotionJobName,
                        options.PromoterVersion,
                        options.PromotionBatchSize,
                        true,
                        [options.RawJobName]),
                    shutdown.Token);
        }

        totalSelected += result.PayloadsSelected;
        totalUpserted += result.RecordsUpserted;
        logger.LogInformation(
            "Curated batch selected {Selected} Raw payloads and upserted {Upserted} objects ({Failed} failures).",
            result.PayloadsSelected,
            result.RecordsUpserted,
            result.PayloadsFailed);
        if (result.PayloadsFailed > 0)
        {
            return 1;
        }

        if (result.PayloadsSelected == 0)
        {
            break;
        }
    }

    logger.LogInformation(
        "Kaggle Raceform sync complete: promoted {Selected} Raw payloads and upserted {Upserted} curated objects.",
        totalSelected,
        totalUpserted);
    return 0;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    logger.LogWarning("Bulk sync was cancelled; completed Raw and Curated audit records were retained.");
    return 2;
}

static DateOnly? ReadDateArgument(string[] arguments, string name)
{
    var value = ReadArgument(arguments, name);
    if (value is null)
    {
        return null;
    }

    return DateOnly.TryParseExact(
        value,
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var date)
        ? date
        : throw new ArgumentException($"{name} must use yyyy-MM-dd.");
}

static string? ReadArgument(string[] arguments, string name)
{
    for (var index = 0; index < arguments.Length; index++)
    {
        if (arguments[index].StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))
        {
            return arguments[index][(name.Length + 1)..];
        }

        if (arguments[index].Equals(name, StringComparison.OrdinalIgnoreCase)
            && index + 1 < arguments.Length)
        {
            return arguments[index + 1];
        }
    }

    return null;
}

static bool HasSwitch(string[] arguments, string name) =>
    arguments.Any(argument => argument.Equals(name, StringComparison.OrdinalIgnoreCase));
