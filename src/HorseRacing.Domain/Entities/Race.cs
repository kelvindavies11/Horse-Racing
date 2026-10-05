using HorseRacing.Domain.Enums;

namespace HorseRacing.Domain.Entities;

public sealed class Race
{
    private readonly List<Runner> _runners = [];

    private Race()
    {
    }

    private Race(Guid id, Guid racecourseId, string name, DateTimeOffset scheduledStartUtc)
    {
        Id = id;
        RacecourseId = racecourseId;
        Name = name;
        ScheduledStartUtc = scheduledStartUtc.ToUniversalTime();
        Status = RaceStatus.Scheduled;
    }

    public Guid Id { get; private set; }

    public Guid RacecourseId { get; private set; }

    public Racecourse Racecourse { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset ScheduledStartUtc { get; private set; }

    public RaceStatus Status { get; private set; }

    public IReadOnlyCollection<Runner> Runners => _runners.AsReadOnly();

    public static Race Create(Guid racecourseId, string name, DateTimeOffset scheduledStartUtc)
    {
        if (racecourseId == Guid.Empty)
        {
            throw new ArgumentException("Racecourse identifier is required.", nameof(racecourseId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Race(Guid.NewGuid(), racecourseId, name.Trim(), scheduledStartUtc);
    }

    public Runner AddRunner(Guid horseId, int clothNumber, decimal? declaredOdds = null)
    {
        if (_runners.Any(runner => runner.HorseId == horseId))
        {
            throw new InvalidOperationException("A horse can only appear once in a race.");
        }

        if (_runners.Any(runner => runner.ClothNumber == clothNumber))
        {
            throw new InvalidOperationException("A cloth number can only appear once in a race.");
        }

        var runner = Runner.Create(Id, horseId, clothNumber, declaredOdds);
        _runners.Add(runner);
        return runner;
    }
}
