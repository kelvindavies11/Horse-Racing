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
            "/entities/{entityId:guid}/connections",
            async (
                Guid entityId,
                int? limit,
                ICuratedReadRepository repository,
                CancellationToken cancellationToken) =>
            {
                var connections = await repository.GetEntityConnectionsAsync(
                    entityId,
                    limit ?? 100,
                    cancellationToken);
                return connections is null
                    ? Results.NotFound()
                    : Results.Ok(connections);
            });

        curated.MapGet(
            "/relationships",
            async (
                int? limit,
                ICuratedReadRepository repository,
                CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetRelationshipsAsync(limit ?? 500, cancellationToken)));

        curated.MapGet(
            "/results/latest-date",
            async (
                ICuratedReadRepository repository,
                CancellationToken cancellationToken) =>
            {
                var latest = await repository.GetLatestRaceDateAsync(cancellationToken)
                    ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
                return Results.Ok(new LatestRaceDateView(latest));
            });

        curated.MapGet(
            "/results",
            async (
                DateOnly? from,
                DateOnly? to,
                ICuratedReadRepository repository,
                CancellationToken cancellationToken) =>
            {
                var end = to
                    ?? (from is not null
                        ? from.Value.AddDays(6)
                        : await repository.GetLatestRaceDateAsync(cancellationToken)
                            ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));
                var start = from ?? end.AddDays(-6);
                if (end < start || end.DayNumber - start.DayNumber > 31)
                {
                    return Results.BadRequest(new
                    {
                        error = "invalid_date_range",
                        message = "The result window must contain between 1 and 32 days."
                    });
                }

                return Results.Ok(await repository.GetRaceResultsAsync(
                    start,
                    end,
                    cancellationToken));
            });

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
