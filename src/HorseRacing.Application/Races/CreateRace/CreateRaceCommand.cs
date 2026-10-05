namespace HorseRacing.Application.Races.CreateRace;

public sealed record CreateRaceCommand(
    Guid RacecourseId,
    string Name,
    DateTimeOffset ScheduledStartUtc);
