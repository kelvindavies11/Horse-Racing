namespace HorseRacing.Infrastructure.Ingestion.Kaggle;

public sealed class KaggleRaceformOptions
{
    public const string SectionName = "KaggleRaceform";

    public string DatasetSlug { get; init; } =
        "deltaromeo/horse-racing-results-ukireland-2015-2025";

    public int DatasetVersion { get; init; } = 118;

    public string DownloadUrl { get; init; } =
        "https://www.kaggle.com/api/v1/datasets/download/deltaromeo/horse-racing-results-ukireland-2015-2025?datasetVersionNumber=118";

    public string ArchiveEntry { get; init; } =
        "form_2015-present/form_2015-present/raceform.db";

    public string CacheDirectory { get; init; } = ".data/kaggle-raceform";

    public string RawJobName { get; init; } = "kaggle-raceform-results";

    public string SourceName { get; init; } = "Kaggle Raceform bulk dataset";

    public string CollectorVersion { get; init; } = "1.0.0";

    public string PromotionJobName { get; init; } = "kaggle-raceform-to-curated";

    public string PromoterVersion { get; init; } = "1.0.0";

    public int PromotionBatchSize { get; init; } = 12;
}
