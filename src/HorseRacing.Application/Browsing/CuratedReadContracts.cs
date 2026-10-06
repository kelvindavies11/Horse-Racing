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
