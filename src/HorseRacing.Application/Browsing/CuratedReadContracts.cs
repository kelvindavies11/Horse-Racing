using System.Text.Json;

namespace HorseRacing.Application.Browsing;

public sealed record CuratedEntityQuery(string? Type, string? Search, int Page, int PageSize);

public sealed record CuratedOverview(
    DateTimeOffset GeneratedAtUtc,
    int TotalEntities,
    int TotalEntityTypes,
    DateTimeOffset? FirstObservedAtUtc,
    DateTimeOffset? LastObservedAtUtc,
    IReadOnlyCollection<EntityTypeSummary> EntityTypes);

public sealed record EntityTypeSummary(string Type, int Count, DateTimeOffset LastObservedAtUtc);

public sealed record CuratedEntityPage(
    DateTimeOffset GeneratedAtUtc,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyCollection<CuratedEntity> Items);

public sealed record CuratedEntity(
    Guid Id,
    string SourceSystem,
    string DomainObjectType,
    string SourceKey,
    string DisplayName,
    string SourceUrl,
    Guid RawPayloadId,
    Guid RawCollectionRunId,
    Guid LastPromotionRunId,
    DateTimeOffset FirstObservedAtUtc,
    DateTimeOffset LastObservedAtUtc,
    JsonElement Data);

public sealed record RelationshipGraph(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyCollection<RelationshipNode> Nodes,
    IReadOnlyCollection<RelationshipEdge> Edges,
    IReadOnlyCollection<RelationshipPattern> Patterns);

public sealed record RelationshipNode(
    Guid Id,
    string Type,
    string DisplayName,
    DateTimeOffset LastObservedAtUtc);

public sealed record RelationshipEdge(Guid SourceId, Guid TargetId, string Label);

public sealed record RelationshipPattern(string SourceType, string TargetType, int Count);

public sealed record RaceResultsFeed(
    DateTimeOffset GeneratedAtUtc,
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalRaces,
    int TotalRunners,
    int WeatherEnrichedRaces,
    IReadOnlyCollection<CuratedRaceResult> Items);

public sealed record CuratedRaceResult(
    Guid Id,
    string SourceKey,
    string RaceName,
    string CourseName,
    DateTimeOffset StartUtc,
    string RaceType,
    int? RaceClass,
    string Distance,
    string Going,
    decimal? PrizeAmount,
    string? PrizeCurrency,
    bool Abandoned,
    string? Winner,
    RacecourseLocationView? Location,
    RaceWeatherView? Weather,
    IReadOnlyCollection<RunnerResultView> Runners);

public sealed record RacecourseLocationView(
    decimal Latitude,
    decimal Longitude,
    string? Postcode,
    string LocationSource);

public sealed record RaceWeatherView(
    DateTimeOffset WeatherHourUtc,
    decimal TemperatureC,
    decimal ApparentTemperatureC,
    int RelativeHumidityPercent,
    decimal PrecipitationMillimetres,
    int WeatherCode,
    decimal WindSpeedKilometresPerHour,
    int WindDirectionDegrees,
    decimal WindGustKilometresPerHour,
    string SourceUrl);

public sealed record RunnerResultView(
    int? FinishPosition,
    string HorseName,
    int? ClothNumber,
    int? Draw,
    string? JockeyName,
    string? TrainerName,
    string? OwnerName,
    string Status,
    string? BettingRatio,
    string? DistanceFromWinner,
    string? FinishTime,
    string? NonRunnerReason,
    string? SilkImageUrl);

public sealed record AuditSnapshot(
    DateTimeOffset GeneratedAtUtc,
    AuditSummary Summary,
    IReadOnlyCollection<RawRunAudit> RawRuns,
    IReadOnlyCollection<PromotionRunAudit> PromotionRuns);

public sealed record AuditSummary(
    int TotalRawRuns,
    int FailedRawRuns,
    int TotalPromotionRuns,
    int FailedPromotionRuns,
    DateTimeOffset? LastRawRunAtUtc,
    DateTimeOffset? LastPromotionRunAtUtc);

public sealed record RawRunAudit(
    Guid Id,
    string JobName,
    string SourceName,
    string SourceUrl,
    string CollectorVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Outcome,
    int? HttpStatusCode,
    Guid? PayloadId,
    long? PayloadBytes,
    string? MediaType,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record PromotionRunAudit(
    Guid Id,
    string JobName,
    string SourceJobName,
    string SourceName,
    string SourceUrl,
    string PromoterVersion,
    Guid RawPayloadId,
    Guid RawCollectionRunId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Outcome,
    int RecordsFound,
    int RecordsUpserted,
    string? ErrorCode,
    string? ErrorMessage);
