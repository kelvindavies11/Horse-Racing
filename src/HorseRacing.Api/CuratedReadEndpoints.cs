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
