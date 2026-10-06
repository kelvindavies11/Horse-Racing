using HorseRacing.Domain.Entities;
using HorseRacing.Infrastructure.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Raw;
using HorseRacing.Infrastructure.Ingestion.Weather;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Persistence;

public sealed class HorseRacingDbContext(DbContextOptions<HorseRacingDbContext> options)
    : DbContext(options)
{
    public DbSet<RawCollectionRun> RawCollectionRuns => Set<RawCollectionRun>();

    public DbSet<RawPayload> RawPayloads => Set<RawPayload>();

    public DbSet<CuratedPromotionRun> CuratedPromotionRuns => Set<CuratedPromotionRun>();

    public DbSet<CuratedDomainObject> CuratedDomainObjects => Set<CuratedDomainObject>();

    public DbSet<CuratedRacecourseLocation> CuratedRacecourseLocations => Set<CuratedRacecourseLocation>();

    public DbSet<CuratedRaceWeather> CuratedRaceWeather => Set<CuratedRaceWeather>();

    public DbSet<Horse> Horses => Set<Horse>();

    public DbSet<Jockey> Jockeys => Set<Jockey>();

    public DbSet<Meeting> Meetings => Set<Meeting>();

    public DbSet<Owner> Owners => Set<Owner>();

    public DbSet<Race> Races => Set<Race>();

    public DbSet<Racecourse> Racecourses => Set<Racecourse>();

    public DbSet<RaceResult> RaceResults => Set<RaceResult>();

    public DbSet<Runner> Runners => Set<Runner>();

    public DbSet<RunnerResult> RunnerResults => Set<RunnerResult>();

    public DbSet<Stable> Stables => Set<Stable>();

    public DbSet<Trainer> Trainers => Set<Trainer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HorseRacingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
