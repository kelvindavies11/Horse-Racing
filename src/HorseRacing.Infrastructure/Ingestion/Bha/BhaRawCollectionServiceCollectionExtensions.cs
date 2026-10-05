using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Infrastructure.Ingestion.Raw;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public static class BhaRawCollectionServiceCollectionExtensions
{
    public static IServiceCollection AddBhaRawCollection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<BhaCollectionOptions>()
            .Bind(configuration.GetSection(BhaCollectionOptions.SectionName))
            .Validate(ValidateOptions, "BHA collection configuration is invalid.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRawIngestionRepository, RawIngestionRepository>();

        services
            .AddHttpClient<BhaPageClient>(ConfigureHttpClient)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3,
                UseCookies = false
            });

        services
            .AddHttpClient<BhaRacecoursesApiClient>(ConfigureHttpClient)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3,
                UseCookies = false
            });

        return services;
    }

    private static void ConfigureHttpClient(
        IServiceProvider serviceProvider,
        HttpClient client)
    {
        var configured = serviceProvider
            .GetRequiredService<IOptions<BhaCollectionOptions>>()
            .Value;

        client.Timeout = TimeSpan.FromSeconds(configured.RequestTimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(configured.UserAgent);
    }

    private static bool ValidateOptions(BhaCollectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CollectorVersion)
            || string.IsNullOrWhiteSpace(options.UserAgent)
            || options.RequestTimeoutSeconds is < 1 or > 120)
        {
            return false;
        }

        return ValidateSourceOptions(options.RacecoursesPage, BhaPageClient.ValidateSourceUri)
            && ValidateSourceOptions(
                options.RacecoursesApi,
                BhaRacecoursesApiClient.ValidateSourceUri);
    }

    private static bool ValidateSourceOptions(
        BhaRawSourceOptions sourceOptions,
        Action<Uri> validateUri)
    {
        if (!sourceOptions.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(sourceOptions.JobName)
            || string.IsNullOrWhiteSpace(sourceOptions.SourceName)
            || sourceOptions.MinimumRequestIntervalSeconds < 10
            || sourceOptions.MaximumResponseBytes is < 1 or > 20_000_000
            || !Uri.TryCreate(sourceOptions.SourceUrl, UriKind.Absolute, out var sourceUri))
        {
            return false;
        }

        try
        {
            validateUri(sourceUri);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
