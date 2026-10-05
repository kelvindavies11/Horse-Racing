using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Persistence;

public sealed class HorseRacingDbContext(DbContextOptions<HorseRacingDbContext> options)
    : DbContext(options)
{
    public DbSet<Horse> Horses => Set<Horse>();

    public DbSet<Race> Races => Set<Race>();

    public DbSet<Racecourse> Racecourses => Set<Racecourse>();

    public DbSet<Runner> Runners => Set<Runner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HorseRacingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
