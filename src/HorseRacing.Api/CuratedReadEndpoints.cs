using HorseRacing.Application.Browsing;

namespace HorseRacing.Api;

public static class CuratedReadEndpoints
{
    public static IEndpointRouteBuilder MapCuratedReadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var curated = endpoints.MapGroup("/api/v1/curated");

        curated.MapGet(
            "/overview",
            async (ICuratedReadRepository repository, CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetOverviewAsync(cancellationToken)));

        curated.MapGet(
            "/entities",
            async (
                string? type,
                string? search,
                int? page,
                int? pageSize,
                ICuratedReadRepository repository,
                CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetEntitiesAsync(
                    new CuratedEntityQuery(type, search, page ?? 1, pageSize ?? 60),
                    cancellationToken)));

        curated.MapGet(
            "/relationships",
            async (
                int? limit,
                ICuratedReadRepository repository,
                CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetRelationshipsAsync(limit ?? 500, cancellationToken)));

        endpoints.MapGet(
            "/api/v1/admin/audit",
            async (
                int? limit,
                ICuratedReadRepository repository,
                CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetAuditAsync(limit ?? 80, cancellationToken)));

        return endpoints;
    }
}
