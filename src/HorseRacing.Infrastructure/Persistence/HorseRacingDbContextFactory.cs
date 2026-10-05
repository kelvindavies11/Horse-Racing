using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HorseRacing.Infrastructure.Persistence;

public sealed class HorseRacingDbContextFactory
    : IDesignTimeDbContextFactory<HorseRacingDbContext>
{
    public HorseRacingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HORSE_RACING_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=horse_racing;Username=horse_racing;Password=horse_racing_local";

        var options = new DbContextOptionsBuilder<HorseRacingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new HorseRacingDbContext(options);
    }
}
