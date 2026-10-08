using System.Globalization;
using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Application.Ingestion.Results;
using HorseRacing.Application.Ingestion.Weather;
using HorseRacing.Infrastructure;
using HorseRacing.Infrastructure.Ingestion.Bha;
using HorseRacing.Infrastructure.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Weather;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddBhaRawCollection(builder.Configuration);
builder.Services.AddBhaCuratedPromotion(builder.Configuration);
builder.Services.AddRaceWeatherEnrichment();

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;
var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("RaceDataSync");
var timeProvider = services.GetRequiredService<TimeProvider>();
var syncConfiguration = builder.Configuration.GetSection("RaceDataSync");
var options = new RaceDataSyncOptions
{
    CollectorVersion = syncConfiguration["CollectorVersion"] ?? "2.0.0",
    PromotionJobName = syncConfiguration["PromotionJobName"] ?? "bha-results-to-curated",
    PromoterVersion = syncConfiguration["PromoterVersion"] ?? "2.0.0",
    DelayBetweenRequestsMilliseconds = int.TryParse(
        syncConfiguration["DelayBetweenRequestsMilliseconds"],
        out var configuredDelay)
        ? Math.Clamp(configuredDelay, 0, 10_000)
        : 1000
};

var defaultTo = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-1);
var toDate = ReadDateArgument(args, "--to") ?? defaultTo;
var fromDate = ReadDateArgument(args, "--from") ?? toDate.AddDays(-6);
if (toDate < fromDate || toDate.DayNumber - fromDate.DayNumber > 31)
{
    logger.LogError("The requested range {FromDate} to {ToDate} must contain between 1 and 32 days.", fromDate, toDate);
    return 2;
}

var mode = (ReadArgument(args, "--mode") ?? "all").ToLowerInvariant();
if (mode is not ("all" or "raw" or "weather"))
{
    logger.LogError("--mode must be all, raw, or weather.");
    return 2;
}

var reuseSuccessfulPayloads = HasSwitch(args, "--reuse-successful");

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

logger.LogInformation(
    "Synchronising BHA race data from {FromDate} to {ToDate} in {Mode} mode.",
    fromDate,
    toDate,
    mode);

try
{
    var failures = 0;
    CollectRaceResultsHistoryResult? collection = null;

    if (mode is "all" or "raw")
    {
        collection = await services.GetRequiredService<CollectRaceResultsHistoryHandler>().HandleAsync(
            new CollectRaceResultsHistoryCommand(
                fromDate,
                toDate,
                options.CollectorVersion,
                TimeSpan.FromMilliseconds(options.DelayBetweenRequestsMilliseconds),
                reuseSuccessfulPayloads),
            shutdown.Token);
        logger.LogInformation(
            "Raw collection found {Fixtures} fixtures and {Races} races, captured {Results} new result payloads, " +
            "reused {Reused} existing payloads, observed {Unavailable} unavailable results, and had {Failures} failed requests.",
            collection.FixturesFound,
            collection.RacesFound,
            collection.ResultPayloadsCollected,
            collection.PayloadsReused,
            collection.UnavailableResultPayloads,
            collection.FailedCollections);
        failures += collection.FailedCollections;
    }

    if (mode == "raw")
    {
        return failures > 0 ? 1 : 0;
    }

    if (mode == "all" && collection is not null)
    {
        var promoter = services.GetRequiredService<PromoteRawPayloadsHandler>();
        var selected = 0;
        var upserted = 0;
        var promotionFailures = 0;
        do
        {
            var promotion = await promoter.HandleAsync(
                new PromoteRawPayloadsCommand(
                    options.PromotionJobName,
                    options.PromoterVersion,
                    1000,
                    true,
                    collection.SuccessfulJobNames),
                shutdown.Token);
            selected += promotion.PayloadsSelected;
            upserted += promotion.RecordsUpserted;
            promotionFailures += promotion.PayloadsFailed;

            if (promotion.PayloadsSelected == 0 || promotion.PayloadsFailed > 0)
            {
                break;
            }
        }
        while (true);

        logger.LogInformation(
            "Curated promotion drained {Selected} payloads and upserted {Upserted} records ({Failed} failures).",
            selected,
            upserted,
            promotionFailures);
        failures += promotionFailures;
    }

    if (mode is "all" or "weather")
    {
        var weather = await services.GetRequiredService<EnrichRaceWeatherHandler>().HandleAsync(
            new EnrichRaceWeatherCommand(
                fromDate,
                toDate,
                options.CollectorVersion,
                TimeSpan.FromMilliseconds(options.DelayBetweenRequestsMilliseconds)),
            shutdown.Token);
        logger.LogInformation(
            "Weather enrichment processed {Targets} races, stored {Locations} locations and {WeatherRows} weather rows ({Failures} failures).",
            weather.RaceTargets,
            weather.LocationsStored,
            weather.WeatherRowsStored,
            weather.FailedCollections);
        failures += weather.FailedCollections;
    }

    return failures > 0 ? 1 : 0;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    logger.LogWarning("Race data synchronisation was cancelled; completed Raw and Curated audit records were retained.");
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

internal sealed class RaceDataSyncOptions
{
    public string CollectorVersion { get; init; } = "2.0.0";
    public string PromotionJobName { get; init; } = "bha-results-to-curated";
    public string PromoterVersion { get; init; } = "2.0.0";
    public int DelayBetweenRequestsMilliseconds { get; init; } = 1000;
}
