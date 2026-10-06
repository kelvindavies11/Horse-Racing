namespace HorseRacing.Infrastructure.Ingestion.Curated;

public sealed class BhaCuratedPromotionOptions
{
    public const string SectionName = "BhaCuratedPromotion";

    public string JobName { get; init; } = "bha-raw-to-curated";

    public string PromoterVersion { get; init; } = "1.0.0";

    public int BatchSize { get; init; } = 100;

    public bool RetryFailedPayloads { get; init; }

    public List<string> SourceJobNames { get; init; } = [];
}
