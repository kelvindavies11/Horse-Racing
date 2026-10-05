using HorseRacing.Application.Abstractions;
using HorseRacing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Persistence.Repositories;

internal sealed class RaceRepository(HorseRacingDbContext dbContext) : IRaceRepository
{
    public async Task AddAsync(Race race, CancellationToken cancellationToken = default)
    {
        await dbContext.Races.AddAsync(race, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Race?> GetByIdAsync(
        Guid raceId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Races
            .Include(race => race.Runners)
            .SingleOrDefaultAsync(race => race.Id == raceId, cancellationToken);
    }
}
