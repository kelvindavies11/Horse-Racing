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

    Task<AuditSnapshot> GetAuditAsync(
        int limit,
        CancellationToken cancellationToken);
}
