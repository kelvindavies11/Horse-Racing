namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaCollectionOptions
{
    public const string SectionName = "BhaCollection";

    public string CollectorVersion { get; init; } = "1.1.0";

    public string UserAgent { get; init; } =
        "HorseRacingLocalCollector/1.1 (+https://github.com/kelvindavies11/Horse-Racing)";

    public int RequestTimeoutSeconds { get; init; } = 30;

    public BhaRawSourceOptions RacecoursesPage { get; init; } = new()
    {
        JobName = "bha-racecourses-page",
        SourceName = "BHA racecourses page",
        SourceUrl = "https://www.britishhorseracing.com/racing/racecourses/",
        MinimumRequestIntervalSeconds = 10,
        MaximumResponseBytes = 5_000_000
    };

    public BhaRawSourceOptions RacecoursesApi { get; init; } = new()
    {
        JobName = "bha-racecourses-api",
        SourceName = "BHA racecourses API",
        SourceUrl = "https://api09.horseracing.software/bha/v1/racecourses/",
        MinimumRequestIntervalSeconds = 10,
        MaximumResponseBytes = 10_000_000
    };
}

public sealed class BhaRawSourceOptions
{
    public bool Enabled { get; init; } = true;

    public string JobName { get; init; } = string.Empty;

    public string SourceName { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public int MinimumRequestIntervalSeconds { get; init; } = 10;

    public int MaximumResponseBytes { get; init; } = 5_000_000;

    public string? BearerToken { get; init; }
}
