using HorseRacing.Domain.Enums;

namespace HorseRacing.Application.Races.CreateRace;

public sealed record CreateRaceCommand(
    Guid MeetingId,
    int RaceNumber,
    string Name,
    DateTimeOffset ScheduledStartUtc,
    RaceCode Code,
    RacingSurface Surface,
    int DistanceMetres);
