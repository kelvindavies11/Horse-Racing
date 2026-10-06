using HorseRacing.Application.Ingestion.Weather;
using Microsoft.Extensions.DependencyInjection;

namespace HorseRacing.Infrastructure.Ingestion.Weather;

public static class RaceWeatherServiceCollectionExtensions
{
    public static IServiceCollection AddRaceWeatherEnrichment(this IServiceCollection services)
    {
        services.AddScoped<IRaceWeatherRepository, RaceWeatherRepository>();
        services.AddScoped<IOpenMeteoPayloadInterpreter, OpenMeteoPayloadInterpreter>();
        services.AddScoped<EnrichRaceWeatherHandler>();
        services
            .AddHttpClient<IOpenMeteoRawSourceClient, OpenMeteoRawSourceClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "HorseRacingLocalCollector/2.0 (+https://github.com/kelvindavies11/Horse-Racing)");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            });
        return services;
    }
}
