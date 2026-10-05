using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Abstractions;

public interface IRaceRepository
{
    Task AddAsync(Race race, CancellationToken cancellationToken = default);

    Task<Race?> GetByIdAsync(Guid raceId, CancellationToken cancellationToken = default);
}
