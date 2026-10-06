using HorseRacing.Application.Ingestion.Curated;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HorseRacing.Infrastructure.Ingestion.Curated;

public static class BhaCuratedPromotionServiceCollectionExtensions
{
    public static IServiceCollection AddBhaCuratedPromotion(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<BhaCuratedPromotionOptions>()
            .Bind(configuration.GetSection(BhaCuratedPromotionOptions.SectionName))
            .Validate(ValidateOptions, "BHA curated promotion configuration is invalid.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICuratedPromotionRepository, CuratedPromotionRepository>();
        services.AddSingleton<ICuratedRawPayloadExtractor, BhaCuratedDomainObjectExtractor>();
        services.AddScoped<PromoteRawPayloadsHandler>();

        return services;
    }

    private static bool ValidateOptions(BhaCuratedPromotionOptions options) =>
        !string.IsNullOrWhiteSpace(options.JobName)
        && !string.IsNullOrWhiteSpace(options.PromoterVersion)
        && options.BatchSize is >= 1 and <= 1000
        && options.SourceJobNames.All(sourceJobName => !string.IsNullOrWhiteSpace(sourceJobName));
}
