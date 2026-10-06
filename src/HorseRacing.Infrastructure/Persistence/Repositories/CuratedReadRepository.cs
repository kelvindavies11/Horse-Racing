using System.Text.Json;
using HorseRacing.Application.Browsing;
using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Persistence.Repositories;

public sealed class CuratedReadRepository(HorseRacingDbContext dbContext) : ICuratedReadRepository
{
    public async Task<CuratedOverview> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var entityTypeRows = await dbContext.CuratedDomainObjects
            .AsNoTracking()
            .GroupBy(domainObject => domainObject.DomainObjectType)
            .Select(group => new
            {
                Type = group.Key,
                Count = group.Count(),
                LastObservedAtUtc = group.Max(domainObject => domainObject.LastObservedAtUtc)
            })
            .OrderByDescending(summary => summary.Count)
            .ThenBy(summary => summary.Type)
            .ToListAsync(cancellationToken);
        var entityTypes = entityTypeRows
            .Select(row => new EntityTypeSummary(row.Type, row.Count, row.LastObservedAtUtc))
            .ToList();

        var firstObservedAtUtc = await dbContext.CuratedDomainObjects
            .AsNoTracking()
            .Select(domainObject => (DateTimeOffset?)domainObject.FirstObservedAtUtc)
            .MinAsync(cancellationToken);
        var lastObservedAtUtc = await dbContext.CuratedDomainObjects
            .AsNoTracking()
            .Select(domainObject => (DateTimeOffset?)domainObject.LastObservedAtUtc)
            .MaxAsync(cancellationToken);

        return new CuratedOverview(
            DateTimeOffset.UtcNow,
            entityTypes.Sum(summary => summary.Count),
            entityTypes.Count,
            firstObservedAtUtc,
            lastObservedAtUtc,
            entityTypes);
    }

    public async Task<CuratedEntityPage> GetEntitiesAsync(
        CuratedEntityQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 12, 100);
        var query = dbContext.CuratedDomainObjects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            query = query.Where(domainObject => domainObject.DomainObjectType == request.Type.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(domainObject =>
                EF.Functions.ILike(domainObject.DisplayName, pattern)
                || EF.Functions.ILike(domainObject.SourceKey, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(domainObject => domainObject.LastObservedAtUtc)
            .ThenBy(domainObject => domainObject.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(domainObject => new CuratedEntityRow(
                domainObject.Id,
                domainObject.SourceSystem,
                domainObject.DomainObjectType,
                domainObject.SourceKey,
                domainObject.DisplayName,
                domainObject.SourceUrl,
                domainObject.RawPayloadId,
                domainObject.RawCollectionRunId,
                domainObject.LastPromotionRunId,
                domainObject.FirstObservedAtUtc,
                domainObject.LastObservedAtUtc,
                domainObject.SourceDataJson))
            .ToListAsync(cancellationToken);

        return new CuratedEntityPage(
            DateTimeOffset.UtcNow,
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize),
            rows.Select(ToEntity).ToList());
    }

    public async Task<RelationshipGraph> GetRelationshipsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var requestedLimit = Math.Clamp(limit, 50, 800);
        var rows = await dbContext.CuratedDomainObjects
            .AsNoTracking()
            .OrderByDescending(domainObject => domainObject.LastObservedAtUtc)
            .Take(requestedLimit)
            .Select(domainObject => new GraphRow(
                domainObject.Id,
                domainObject.DomainObjectType,
                domainObject.SourceKey,
                domainObject.DisplayName,
                domainObject.LastObservedAtUtc,
                domainObject.SourceDataJson))
            .ToListAsync(cancellationToken);

        var edges = InferRelationships(rows);
        var nodeTypes = rows.ToDictionary(row => row.Id, row => row.DomainObjectType);
        var patterns = edges
            .GroupBy(edge => new
            {
                SourceType = nodeTypes[edge.SourceId],
                TargetType = nodeTypes[edge.TargetId]
            })
            .Select(group => new RelationshipPattern(
                group.Key.SourceType,
                group.Key.TargetType,
                group.Count()))
            .OrderByDescending(pattern => pattern.Count)
            .ThenBy(pattern => pattern.SourceType)
            .ToList();

        return new RelationshipGraph(
            DateTimeOffset.UtcNow,
            rows.Select(row => new RelationshipNode(
                row.Id,
                row.DomainObjectType,
                row.DisplayName,
                row.LastObservedAtUtc)).ToList(),
            edges,
            patterns);
    }

    public async Task<AuditSnapshot> GetAuditAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var requestedLimit = Math.Clamp(limit, 20, 200);
        var totalRawRuns = await dbContext.RawCollectionRuns.CountAsync(cancellationToken);
        var failedRawRuns = await dbContext.RawCollectionRuns
            .CountAsync(run => run.Outcome == RawCollectionOutcome.Failed, cancellationToken);
        var totalPromotionRuns = await dbContext.CuratedPromotionRuns.CountAsync(cancellationToken);
        var failedPromotionRuns = await dbContext.CuratedPromotionRuns
            .CountAsync(run => run.Outcome == CuratedPromotionOutcome.Failed, cancellationToken);

        var rawRuns = await dbContext.RawCollectionRuns
            .AsNoTracking()
            .Include(run => run.Payload)
            .OrderByDescending(run => run.StartedAtUtc)
            .Take(requestedLimit)
            .ToListAsync(cancellationToken);
        var promotionRuns = await dbContext.CuratedPromotionRuns
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAtUtc)
            .Take(requestedLimit)
            .ToListAsync(cancellationToken);

        return new AuditSnapshot(
            DateTimeOffset.UtcNow,
            new AuditSummary(
                totalRawRuns,
                failedRawRuns,
                totalPromotionRuns,
                failedPromotionRuns,
                rawRuns.FirstOrDefault()?.StartedAtUtc,
                promotionRuns.FirstOrDefault()?.StartedAtUtc),
            rawRuns.Select(run => new RawRunAudit(
                run.Id,
                run.JobName,
                run.SourceName,
                run.SourceUrl,
                run.CollectorVersion,
                run.StartedAtUtc,
                run.CompletedAtUtc,
                run.Outcome.ToString(),
                run.HttpStatusCode,
                run.Payload?.Id,
                run.Payload?.ContentLength,
                run.Payload?.MediaType,
                run.ErrorCode,
                run.ErrorMessage)).ToList(),
            promotionRuns.Select(run => new PromotionRunAudit(
                run.Id,
                run.JobName,
                run.SourceJobName,
                run.SourceName,
                run.SourceUrl,
                run.PromoterVersion,
                run.RawPayloadId,
                run.RawCollectionRunId,
                run.StartedAtUtc,
                run.CompletedAtUtc,
                run.Outcome.ToString(),
                run.RecordsFound,
                run.RecordsUpserted,
                run.ErrorCode,
                run.ErrorMessage)).ToList());
    }

    private static CuratedEntity ToEntity(CuratedEntityRow row) =>
        new(
            row.Id,
            row.SourceSystem,
            row.DomainObjectType,
            row.SourceKey,
            row.DisplayName,
            row.SourceUrl,
            row.RawPayloadId,
            row.RawCollectionRunId,
            row.LastPromotionRunId,
            row.FirstObservedAtUtc,
            row.LastObservedAtUtc,
            ParseFoundData(row.SourceDataJson));

    private static JsonElement ParseFoundData(string sourceDataJson)
    {
        using var document = JsonDocument.Parse(sourceDataJson);
        return document.RootElement.TryGetProperty("foundData", out var foundData)
            ? foundData.Clone()
            : document.RootElement.Clone();
    }

    private static List<RelationshipEdge> InferRelationships(IReadOnlyCollection<GraphRow> rows)
    {
        var lookup = new Dictionary<string, List<GraphRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            AddLookup(lookup, row.DomainObjectType, row.SourceKey, row);
            AddLookup(lookup, row.DomainObjectType, row.DisplayName, row);
        }

        var edges = new List<RelationshipEdge>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            using var document = JsonDocument.Parse(row.SourceDataJson);
            var root = document.RootElement.TryGetProperty("foundData", out var foundData)
                ? foundData
                : document.RootElement;

            foreach (var reference in EnumerateReferences(root))
            {
                var targetType = InferTargetType(reference.PropertyName);
                if (targetType is null
                    || !lookup.TryGetValue(CreateLookupKey(targetType, reference.Value), out var targets))
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    if (target.Id == row.Id)
                    {
                        continue;
                    }

                    var key = string.CompareOrdinal(row.Id.ToString(), target.Id.ToString()) < 0
                        ? $"{row.Id}:{target.Id}"
                        : $"{target.Id}:{row.Id}";
                    if (seen.Add(key))
                    {
                        edges.Add(new RelationshipEdge(
                            row.Id,
                            target.Id,
                            HumanisePropertyName(reference.PropertyName)));
                    }
                }
            }
        }

        return edges;
    }

    private static IEnumerable<EntityReference> EnumerateReferences(JsonElement element, int depth = 0)
    {
        if (depth > 4)
        {
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                {
                    var value = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : property.Value.GetRawText();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return new EntityReference(property.Name, value);
                    }
                }
                else
                {
                    foreach (var nested in EnumerateReferences(property.Value, depth + 1))
                    {
                        yield return nested;
                    }
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in EnumerateReferences(item, depth + 1))
                {
                    yield return nested;
                }
            }
        }
    }

    private static string? InferTargetType(string propertyName)
    {
        var name = new string(propertyName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (name.Contains("racecourse", StringComparison.Ordinal) || name is "courseid" or "coursecode" or "coursename") return "Racecourse";
        if (name.Contains("fixture", StringComparison.Ordinal) || name.Contains("meeting", StringComparison.Ordinal)) return "Meeting";
        if (name.Contains("racehorse", StringComparison.Ordinal) || name.Contains("horse", StringComparison.Ordinal)) return "Horse";
        if (name.Contains("jockey", StringComparison.Ordinal)) return "Jockey";
        if (name.Contains("trainer", StringComparison.Ordinal)) return "Trainer";
        if (name.Contains("owner", StringComparison.Ordinal)) return "Owner";
        if (name.Contains("runner", StringComparison.Ordinal)) return "Runner";
        if (name.StartsWith("race", StringComparison.Ordinal) && !name.StartsWith("racing", StringComparison.Ordinal)) return "Race";
        return null;
    }

    private static void AddLookup(
        IDictionary<string, List<GraphRow>> lookup,
        string type,
        string value,
        GraphRow row)
    {
        var key = CreateLookupKey(type, value);
        if (!lookup.TryGetValue(key, out var matches))
        {
            matches = [];
            lookup[key] = matches;
        }

        matches.Add(row);
    }

    private static string CreateLookupKey(string type, string value) => $"{type}:{value.Trim()}";

    private static string HumanisePropertyName(string propertyName)
    {
        var words = new List<char>(propertyName.Length + 4);
        foreach (var character in propertyName)
        {
            if (char.IsUpper(character) && words.Count > 0 && words[^1] != ' ')
            {
                words.Add(' ');
            }

            words.Add(character is '_' or '-' ? ' ' : char.ToLowerInvariant(character));
        }

        return new string(words.ToArray()).Trim();
    }

    private sealed record CuratedEntityRow(
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
        string SourceDataJson);

    private sealed record GraphRow(
        Guid Id,
        string DomainObjectType,
        string SourceKey,
        string DisplayName,
        DateTimeOffset LastObservedAtUtc,
        string SourceDataJson);

    private sealed record EntityReference(string PropertyName, string Value);
}
