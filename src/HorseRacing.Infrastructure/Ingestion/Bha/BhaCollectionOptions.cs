namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaCollectionOptions
{
    public const string SectionName = "BhaCollection";

    public string CollectorVersion { get; init; } = "1.5.0";

    public string UserAgent { get; init; } =
        "HorseRacingLocalCollector/1.5 (+https://github.com/kelvindavies11/Horse-Racing)";

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

    public BhaRawSourceOptions FixturesPage { get; init; } = new()
    {
        JobName = "bha-fixtures-page",
        SourceName = "BHA full-year fixtures page",
        SourceUrl = "https://www.britishhorseracing.com/racing/fixtures/full-year/",
        MinimumRequestIntervalSeconds = 10,
        MaximumResponseBytes = 5_000_000
    };

    public BhaRawSourceOptions FixturesApi { get; init; } = new()
    {
        JobName = "bha-fixtures-api",
        SourceName = "BHA fixtures API",
        SourceUrl = "https://api09.horseracing.software/bha/v1/fixtures?per_page=250",
        MinimumRequestIntervalSeconds = 10,
        MaximumResponseBytes = 20_000_000
    };

    public List<BhaRawSourceOptions> FixtureCalendarSources { get; init; } =
    [
        new()
        {
            JobName = "bha-fixtures-calendar-2026",
            SourceName = "BHA 2026 fixtures calendar",
            SourceUrl = "https://crate.horseracing.software/ics/fixtures?year=2026",
            MinimumRequestIntervalSeconds = 10,
            MaximumResponseBytes = 5_000_000
        },
        new()
        {
            JobName = "bha-fixtures-calendar-2027",
            SourceName = "BHA 2027 fixtures calendar",
            SourceUrl = "https://crate.horseracing.software/ics/fixtures?year=2027",
            MinimumRequestIntervalSeconds = 10,
            MaximumResponseBytes = 5_000_000
        }
    ];

    public List<BhaRawSourceOptions> FixtureListDownloadSources { get; init; } =
    [
        new()
        {
            JobName = "bha-fixtures-list-2027-xlsx",
            SourceName = "BHA 2027 fixture list XLSX",
            SourceUrl = "https://media.britishhorseracing.com/bha/Fixture_List/2027-Fixture-list.xlsx",
            MinimumRequestIntervalSeconds = 10,
            MaximumResponseBytes = 20_000_000
        },
        new()
        {
            JobName = "bha-fixtures-list-2027-pdf",
            SourceName = "BHA 2027 fixture list PDF",
            SourceUrl = "https://media.britishhorseracing.com/bha/Fixture_List/2027_Fixture_List.pdf",
            MinimumRequestIntervalSeconds = 10,
            MaximumResponseBytes = 20_000_000
        }
    ];

    public BhaRawSourceOptions UpcomingFixturesPage { get; init; } = new()
    {
        JobName = "bha-upcoming-fixtures-page",
        SourceName = "BHA upcoming fixtures page",
        SourceUrl = "https://www.britishhorseracing.com/racing/fixtures/upcoming/",
        MinimumRequestIntervalSeconds = 10,
        MaximumResponseBytes = 5_000_000
    };

    public BhaRawSourceOptions RacecardPage { get; init; } = new()
    {
        JobName = "bha-racecard-page",
        SourceName = "BHA racecard page shell",
        SourceUrl = "https://www.britishhorseracing.com/racing/fixtures/upcoming/racecard/race/",
        MinimumRequestIntervalSeconds = 10,
        MaximumResponseBytes = 5_000_000
    };

    public string? RacecardApiBearerToken { get; init; }

    public List<BhaRawSourceOptions> RacecardApiSources { get; init; } = [];

    public BhaRawSourceOptions RacehorseSearchPage { get; init; } = new()
    {
        JobName = "bha-racehorse-search-page",
        SourceName = "BHA racehorse search page",
        SourceUrl = "https://www.britishhorseracing.com/racing/horses/racehorse-search-results/",
        MinimumRequestIntervalSeconds = 10,
        MaximumResponseBytes = 5_000_000
    };

    public string? RacehorseApiBearerToken { get; init; }

    public List<BhaRawSourceOptions> RacehorseApiSources { get; init; } = [];

    public List<BhaRawSourceOptions> RacingStatusPageSources { get; init; } =
    [
        new()
        {
            JobName = "bha-results-page",
            SourceName = "BHA results page",
            SourceUrl = "https://www.britishhorseracing.com/racing/results/",
            MinimumRequestIntervalSeconds = 10,
            MaximumResponseBytes = 5_000_000
        },
        new()
        {
            JobName = "bha-stewards-reports-page",
            SourceName = "BHA stewards reports page",
            SourceUrl = "https://www.britishhorseracing.com/racing/stewards-reports/",
            MinimumRequestIntervalSeconds = 10,
            MaximumResponseBytes = 5_000_000
        },
        new()
        {
            JobName = "bha-racing-updates-page",
            SourceName = "BHA racing updates page",
            SourceUrl = "https://www.britishhorseracing.com/racing/racing-updates/",
            MinimumRequestIntervalSeconds = 10,
            MaximumResponseBytes = 5_000_000
        }
    ];

    public string? RacingStatusApiBearerToken { get; init; }

    public List<BhaRawSourceOptions> RacingStatusApiSources { get; init; } =
    [
        new()
        {
            JobName = "bha-results-api",
            SourceName = "BHA results API",
            SourceUrl = "https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=1&fields=courseId,courseName,fixtureDate,fixtureType,fixtureSession,abandonedReasonCode,highlightTitle",
            MinimumRequestIntervalSeconds = 10,
            MaximumResponseBytes = 20_000_000
        },
        new()
        {
            JobName = "bha-stewards-reports-api",
            SourceName = "BHA stewards reports API",
            SourceUrl = "https://api09.horseracing.software/bha/v1/stewards-room/reports",
            MinimumRequestIntervalSeconds = 10,
            MaximumResponseBytes = 20_000_000
        }
    ];
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
