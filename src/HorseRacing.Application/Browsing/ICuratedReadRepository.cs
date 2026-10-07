namespace HorseRacing.Application.Browsing;

public interface ICuratedReadRepository
{
    Task<CuratedOverview> GetOverviewAsync(CancellationToken cancellationToken);

    Task<CuratedEntityPage> GetEntitiesAsync(
        CuratedEntityQuery query,
        CancellationToken cancellationToken);

    Task<RelationshipGraph> GetRelationshipsAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<DateOnly?> GetLatestRaceDateAsync(CancellationToken cancellationToken);

    Task<RaceResultsFeed> GetRaceResultsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);

    Task<AuditSnapshot> GetAuditAsync(
        int limit,
        CancellationToken cancellationToken);
}
