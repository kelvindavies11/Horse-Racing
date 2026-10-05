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
var repository = scope.ServiceProvider.GetRequiredService<IRawIngestionRepository>();
var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

logger.LogInformation(
    "Starting BHA raw collection. This collector is restricted to local, non-commercial use " +
    "of reviewed BHA racecourse, fixture, racecard, racehorse and racing status sources.");

var sources = new List<RawSourceToCollect>();

if (options.RacecoursesPage.Enabled)
{
    sources.Add(new RawSourceToCollect(
        options.RacecoursesPage,
        scope.ServiceProvider.GetRequiredService<BhaPageClient>()));
}

if (options.RacecoursesApi.Enabled)
{
    sources.Add(new RawSourceToCollect(
        options.RacecoursesApi,
        scope.ServiceProvider.GetRequiredService<BhaRacecoursesApiClient>()));
}

if (options.FixturesPage.Enabled)
{
    sources.Add(new RawSourceToCollect(
        options.FixturesPage,
        scope.ServiceProvider.GetRequiredService<BhaFixturesPageClient>()));
}

if (options.FixturesApi.Enabled)
{
    sources.Add(new RawSourceToCollect(
        options.FixturesApi,
        scope.ServiceProvider.GetRequiredService<BhaFixturesApiClient>()));
}

var fixtureCalendarClient =
    scope.ServiceProvider.GetRequiredService<BhaFixtureCalendarClient>();

foreach (var calendarSource in options.FixtureCalendarSources.Where(source => source.Enabled))
{
    sources.Add(new RawSourceToCollect(calendarSource, fixtureCalendarClient));
}

var fixtureListDownloadClient =
    scope.ServiceProvider.GetRequiredService<BhaFixtureListDownloadClient>();

foreach (var downloadSource in options.FixtureListDownloadSources.Where(source => source.Enabled))
{
    sources.Add(new RawSourceToCollect(downloadSource, fixtureListDownloadClient));
}

if (options.UpcomingFixturesPage.Enabled)
{
    sources.Add(new RawSourceToCollect(
        options.UpcomingFixturesPage,
        scope.ServiceProvider.GetRequiredService<BhaUpcomingFixturesPageClient>()));
}

if (options.RacecardPage.Enabled)
{
    sources.Add(new RawSourceToCollect(
        options.RacecardPage,
        scope.ServiceProvider.GetRequiredService<BhaRacecardPageClient>()));
}

var racecardApiClient = scope.ServiceProvider.GetRequiredService<BhaRacecardApiClient>();

foreach (var racecardApiSource in options.RacecardApiSources.Where(source => source.Enabled))
{
    sources.Add(new RawSourceToCollect(racecardApiSource, racecardApiClient));
}

if (options.RacehorseSearchPage.Enabled)
{
    sources.Add(new RawSourceToCollect(
        options.RacehorseSearchPage,
        scope.ServiceProvider.GetRequiredService<BhaRacehorseSearchPageClient>()));
}

var racehorseApiClient = scope.ServiceProvider.GetRequiredService<BhaRacehorseApiClient>();

foreach (var racehorseApiSource in options.RacehorseApiSources.Where(source => source.Enabled))
{
    sources.Add(new RawSourceToCollect(racehorseApiSource, racehorseApiClient));
}

var racingStatusPageClient =
    scope.ServiceProvider.GetRequiredService<BhaRacingStatusPageClient>();

foreach (var statusPageSource in options.RacingStatusPageSources.Where(source => source.Enabled))
{
    sources.Add(new RawSourceToCollect(statusPageSource, racingStatusPageClient));
}

var racingStatusApiClient =
    scope.ServiceProvider.GetRequiredService<BhaRacingStatusApiClient>();

foreach (var statusApiSource in options.RacingStatusApiSources.Where(source => source.Enabled))
{
    sources.Add(new RawSourceToCollect(statusApiSource, racingStatusApiClient));
}

if (sources.Count == 0)
{
    logger.LogError("No BHA raw sources are enabled.");
    return 1;
}

var hasFailure = false;

foreach (var source in sources)
{
    logger.LogInformation("Collecting {JobName} from {SourceUrl}.", source.Options.JobName, source.Options.SourceUrl);

    var handler = new CollectRawSourceHandler(source.Client, repository, timeProvider);
    RawCollectionResult result;

    try
    {
        result = await handler.HandleAsync(
            new CollectRawSourceCommand(
                source.Options.JobName,
                source.Options.SourceName,
                new Uri(source.Options.SourceUrl),
                options.CollectorVersion,
                TimeSpan.FromSeconds(source.Options.MinimumRequestIntervalSeconds)),
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
            "Collection run {RunId} stored Raw payload {PayloadId} for {JobName} (HTTP {StatusCode}).",
            result.RunId,
            result.PayloadId,
            source.Options.JobName,
            result.HttpStatusCode);

        continue;
    }

    hasFailure = true;
    logger.LogError(
        "Collection run {RunId} for {JobName} failed with {ErrorCode}: {ErrorMessage}",
        result.RunId,
        source.Options.JobName,
        result.ErrorCode,
        result.ErrorMessage);
}

return hasFailure ? 1 : 0;

internal sealed record RawSourceToCollect(
    BhaRawSourceOptions Options,
    IRawSourceClient Client);
