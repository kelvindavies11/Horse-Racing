using HorseRacing.Application.Abstractions;
using HorseRacing.Domain.Entities;

namespace HorseRacing.Application.Races.CreateRace;

public sealed class CreateRaceHandler(IRaceRepository raceRepository)
{
    public async Task<Guid> HandleAsync(
        CreateRaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var race = Race.Create(
            command.MeetingId,
            command.RaceNumber,
            command.Name,
            command.ScheduledStartUtc,
            command.Code,
            command.Surface,
            command.DistanceMetres);

        await raceRepository.AddAsync(race, cancellationToken);
        return race.Id;
    }
}
