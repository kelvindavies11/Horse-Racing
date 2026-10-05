namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaCollectionOptions
{
    public const string SectionName = "BhaCollection";

    public string JobName { get; init; } = "bha-racecourses-page";

    public string SourceName { get; init; } = "BHA racecourses page";

    public string SourceUrl { get; init; } =
        "https://www.britishhorseracing.com/racing/racecourses/";

    public string CollectorVersion { get; init; } = "1.0.0";

    public string UserAgent { get; init; } =
        "HorseRacingLocalCollector/1.0 (+https://github.com/kelvindavies11/Horse-Racing)";

    public int RequestTimeoutSeconds { get; init; } = 30;

    public int MinimumRequestIntervalSeconds { get; init; } = 10;

    public int MaximumResponseBytes { get; init; } = 5_000_000;
}
