using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Infrastructure.Ingestion.Raw;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HorseRacing.Infrastructure.Ingestion.Kaggle;

public static class KaggleRaceformServiceCollectionExtensions
{
    public static IServiceCollection AddKaggleRaceformImport(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<KaggleRaceformOptions>()
            .Bind(configuration.GetSection(KaggleRaceformOptions.SectionName))
            .Validate(ValidateOptions, "Kaggle Raceform configuration is invalid.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRawIngestionRepository, RawIngestionRepository>();
        services.AddScoped<CaptureRawPayloadHandler>();
        services.AddSingleton<KaggleRaceformReader>();
        services
            .AddHttpClient<KaggleRaceformArchiveDownloader>(client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("HorseRacingBulkImporter/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                UseCookies = false
            });

        return services;
    }

    private static bool ValidateOptions(KaggleRaceformOptions options)
    {
        if (options.DatasetVersion <= 0
            || string.IsNullOrWhiteSpace(options.DatasetSlug)
            || string.IsNullOrWhiteSpace(options.ArchiveEntry)
            || string.IsNullOrWhiteSpace(options.CacheDirectory)
            || string.IsNullOrWhiteSpace(options.RawJobName)
            || string.IsNullOrWhiteSpace(options.SourceName)
            || string.IsNullOrWhiteSpace(options.CollectorVersion)
            || string.IsNullOrWhiteSpace(options.PromotionJobName)
            || string.IsNullOrWhiteSpace(options.PromoterVersion)
            || options.PromotionBatchSize is < 1 or > 1000
            || !Uri.TryCreate(options.DownloadUrl, UriKind.Absolute, out var downloadUri))
        {
            return false;
        }

        return downloadUri.Scheme == Uri.UriSchemeHttps
            && downloadUri.Host.Equals("www.kaggle.com", StringComparison.OrdinalIgnoreCase);
    }
}
