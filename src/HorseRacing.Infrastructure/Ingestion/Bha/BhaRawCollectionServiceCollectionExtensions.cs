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
        services.AddScoped<CollectRawSourceHandler>();

        services
            .AddHttpClient<IRawSourceClient, BhaPageClient>((serviceProvider, client) =>
            {
                var configured = serviceProvider
                    .GetRequiredService<IOptions<BhaCollectionOptions>>()
                    .Value;

                client.Timeout = TimeSpan.FromSeconds(configured.RequestTimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(configured.UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3,
                UseCookies = false
            });

        return services;
    }

    private static bool ValidateOptions(BhaCollectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.JobName)
            || string.IsNullOrWhiteSpace(options.SourceName)
            || string.IsNullOrWhiteSpace(options.CollectorVersion)
            || string.IsNullOrWhiteSpace(options.UserAgent)
            || options.RequestTimeoutSeconds is < 1 or > 120
            || options.MinimumRequestIntervalSeconds < 10
            || options.MaximumResponseBytes is < 1 or > 20_000_000
            || !Uri.TryCreate(options.SourceUrl, UriKind.Absolute, out var sourceUri))
        {
            return false;
        }

        try
        {
            BhaPageClient.ValidateSourceUri(sourceUri);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
