using HorseRacing.Domain.Enums;

namespace HorseRacing.Domain.Entities;

public sealed class Race
{
    private readonly List<Runner> _runners = [];

    private Race()
    {
    }

    private Race(
        Guid id,
        Guid meetingId,
        int raceNumber,
        string name,
        DateTimeOffset scheduledStartUtc,
        RaceCode code,
        RacingSurface surface,
        int distanceMetres)
    {
        Id = id;
        MeetingId = meetingId;
        RaceNumber = raceNumber;
        Name = name;
        ScheduledStartUtc = scheduledStartUtc.ToUniversalTime();
        Code = code;
        Surface = surface;
        DistanceMetres = distanceMetres;
        Status = RaceStatus.Scheduled;
    }

    public Guid Id { get; private set; }

    public Guid MeetingId { get; private set; }

    public Meeting Meeting { get; private set; } = null!;

    public int RaceNumber { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset ScheduledStartUtc { get; private set; }

    public RaceCode Code { get; private set; }

    public RacingSurface Surface { get; private set; }

    public int DistanceMetres { get; private set; }

    public string? GoingDescription { get; private set; }

    public RaceStatus Status { get; private set; }

    public IReadOnlyCollection<Runner> Runners => _runners.AsReadOnly();

    public RaceResult? Result { get; private set; }

    public static Race Create(
        Guid meetingId,
        int raceNumber,
        string name,
        DateTimeOffset scheduledStartUtc,
        RaceCode code,
        RacingSurface surface,
        int distanceMetres)
    {
        if (meetingId == Guid.Empty)
        {
            throw new ArgumentException("Meeting identifier is required.", nameof(meetingId));
        }

        if (raceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(raceNumber), "Race number must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Enum.IsDefined(code)) throw new ArgumentOutOfRangeException(nameof(code));
        if (!Enum.IsDefined(surface)) throw new ArgumentOutOfRangeException(nameof(surface));

        if (distanceMetres <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(distanceMetres),
                "Race distance must be positive.");
        }

        return new Race(
            Guid.NewGuid(),
            meetingId,
            raceNumber,
            name.Trim(),
            scheduledStartUtc,
            code,
            surface,
            distanceMetres);
    }

    public Runner AddRunner(
        Guid horseId,
        Guid trainerId,
        Guid ownerId,
        int clothNumber,
        Guid? jockeyId = null,
        int? draw = null,
        int? carriedWeightPounds = null,
        decimal? declaredOdds = null,
        Guid? stableId = null)
    {
        if (Status != RaceStatus.Scheduled)
        {
            throw new InvalidOperationException("Runners can only be added before a race starts.");
        }
        if (_runners.Any(runner => runner.HorseId == horseId))
        {
            throw new InvalidOperationException("A horse can only appear once in a race.");
        }

        if (_runners.Any(runner => runner.ClothNumber == clothNumber))
        {
            throw new InvalidOperationException("A cloth number can only appear once in a race.");
        }

        if (draw is not null && _runners.Any(runner => runner.Draw == draw))
        {
            throw new InvalidOperationException("A draw number can only appear once in a race.");
        }

        var runner = Runner.Create(
            Id,
            horseId,
            trainerId,
            ownerId,
            jockeyId,
            clothNumber,
            draw,
            carriedWeightPounds,
            declaredOdds,
            stableId);
        runner.AttachTo(this);
        _runners.Add(runner);
        return runner;
    }

    public void SetGoing(string goingDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goingDescription);
        GoingDescription = goingDescription.Trim();
    }

    public void MarkOff()
    {
        if (Status != RaceStatus.Scheduled)
        {
            throw new InvalidOperationException("Only a scheduled race can be marked off.");
        }

        Status = RaceStatus.Off;
    }

    public RaceResult CreateResult(
        DateTimeOffset publishedAtUtc,
        ResultStatus status,
        TimeSpan? winningTime = null)
    {
        if (Status == RaceStatus.Abandoned)
        {
            throw new InvalidOperationException("An abandoned race cannot have a result.");
        }

        if (Result is not null)
        {
            throw new InvalidOperationException("A race can only have one result record.");
        }

        if (status != ResultStatus.Provisional)
        {
            throw new ArgumentException("Assemble a provisional result before making it official.", nameof(status));
        }

        Result = RaceResult.Create(Id, publishedAtUtc, status, winningTime);
        Result.AttachTo(this);
        Status = RaceStatus.Finished;
        return Result;
    }

    public void Abandon()
    {
        if (Status == RaceStatus.Finished)
        {
            throw new InvalidOperationException("A finished race cannot be abandoned.");
        }

        Status = RaceStatus.Abandoned;
    }
}
