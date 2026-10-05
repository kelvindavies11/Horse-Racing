using HorseRacing.Domain.Enums;

namespace HorseRacing.Domain.Entities;

public sealed class RunnerResult
{
    private RunnerResult()
    {
    }

    private RunnerResult(
        Guid id,
        Guid raceResultId,
        Guid raceId,
        Guid runnerId,
        RunnerOutcome outcome,
        int? finishPosition,
        bool isDeadHeat,
        decimal? distanceBeatenLengths,
        decimal? startingPriceDecimal,
        decimal? prizeMoney)
    {
        Id = id;
        RaceResultId = raceResultId;
        RaceId = raceId;
        RunnerId = runnerId;
        Outcome = outcome;
        FinishPosition = finishPosition;
        IsDeadHeat = isDeadHeat;
        DistanceBeatenLengths = distanceBeatenLengths;
        StartingPriceDecimal = startingPriceDecimal;
        PrizeMoney = prizeMoney;
    }

    public Guid Id { get; private set; }

    public Guid RaceResultId { get; private set; }

    public Guid RaceId { get; private set; }

    public RaceResult RaceResult { get; private set; } = null!;

    public Guid RunnerId { get; private set; }

    public Runner Runner { get; private set; } = null!;

    public RunnerOutcome Outcome { get; private set; }

    public int? FinishPosition { get; private set; }

    public bool IsDeadHeat { get; private set; }

    public decimal? DistanceBeatenLengths { get; private set; }

    public decimal? StartingPriceDecimal { get; private set; }

    public decimal? PrizeMoney { get; private set; }

    internal static RunnerResult Create(
        Guid raceResultId,
        Guid raceId,
        Guid runnerId,
        RunnerOutcome outcome,
        int? finishPosition,
        bool isDeadHeat,
        decimal? distanceBeatenLengths,
        decimal? startingPriceDecimal,
        decimal? prizeMoney)
    {
        if (raceResultId == Guid.Empty)
        {
            throw new ArgumentException("Race result identifier is required.", nameof(raceResultId));
        }

        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));

        if (runnerId == Guid.Empty)
        {
            throw new ArgumentException("Runner identifier is required.", nameof(runnerId));
        }

        if (outcome == RunnerOutcome.Finished && finishPosition is null)
        {
            throw new ArgumentException(
                "A finishing position is required for a runner that finished.",
                nameof(finishPosition));
        }

        if (outcome != RunnerOutcome.Finished && finishPosition is not null)
        {
            throw new ArgumentException(
                "A non-finisher cannot have a finishing position.",
                nameof(finishPosition));
        }

        if (finishPosition is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finishPosition),
                "Finish position must be positive.");
        }

        if (isDeadHeat && outcome != RunnerOutcome.Finished)
        {
            throw new ArgumentException(
                "Only a finisher can be marked as a dead heat.",
                nameof(isDeadHeat));
        }

        if (distanceBeatenLengths is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(distanceBeatenLengths),
                "Distance beaten cannot be negative.");
        }

        if (startingPriceDecimal is <= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingPriceDecimal),
                "Decimal starting price must exceed one.");
        }

        if (prizeMoney is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prizeMoney), "Prize money cannot be negative.");
        }

        return new RunnerResult(
            Guid.NewGuid(),
            raceResultId,
            raceId,
            runnerId,
            outcome,
            finishPosition,
            isDeadHeat,
            distanceBeatenLengths,
            startingPriceDecimal,
            prizeMoney);
    }
}
