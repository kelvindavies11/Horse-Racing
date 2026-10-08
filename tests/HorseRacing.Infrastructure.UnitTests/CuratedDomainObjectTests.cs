using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Curated;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class CuratedDomainObjectTests
{
    [Fact]
    public void Older_observation_expands_first_seen_without_replacing_latest_values()
    {
        var latestObservedAt = new DateTimeOffset(2026, 10, 8, 8, 0, 0, TimeSpan.Zero);
        var olderObservedAt = latestObservedAt.AddDays(-1);
        var latestPayloadId = Guid.NewGuid();
        var latestRunId = Guid.NewGuid();
        var latestPromotionId = Guid.NewGuid();
        var item = CuratedDomainObject.Create(
            Candidate("Latest name", latestObservedAt),
            latestPayloadId,
            latestRunId,
            latestPromotionId);

        item.ApplyObservation(
            Candidate("Older name", olderObservedAt),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.Equal(olderObservedAt, item.FirstObservedAtUtc);
        Assert.Equal(latestObservedAt, item.LastObservedAtUtc);
        Assert.Equal("Latest name", item.DisplayName);
        Assert.Equal(latestPayloadId, item.RawPayloadId);
        Assert.Equal(latestRunId, item.RawCollectionRunId);
        Assert.Equal(latestPromotionId, item.LastPromotionRunId);
    }

    [Fact]
    public void Newer_observation_replaces_latest_values_without_moving_first_seen()
    {
        var firstObservedAt = new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);
        var latestObservedAt = firstObservedAt.AddDays(1);
        var latestPayloadId = Guid.NewGuid();
        var item = CuratedDomainObject.Create(
            Candidate("First name", firstObservedAt),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        item.ApplyObservation(
            Candidate("Latest name", latestObservedAt),
            latestPayloadId,
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.Equal(firstObservedAt, item.FirstObservedAtUtc);
        Assert.Equal(latestObservedAt, item.LastObservedAtUtc);
        Assert.Equal("Latest name", item.DisplayName);
        Assert.Equal(latestPayloadId, item.RawPayloadId);
    }

    private static CuratedDomainObjectCandidate Candidate(
        string displayName,
        DateTimeOffset observedAtUtc) =>
        new(
            "BHA",
            "Horse",
            "horse-1",
            displayName,
            new Uri("https://example.test/horses/1"),
            observedAtUtc,
            $"{{\"name\":\"{displayName}\"}}");
}
