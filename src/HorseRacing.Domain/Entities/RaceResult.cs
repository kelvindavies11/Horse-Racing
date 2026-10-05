using HorseRacing.Domain.Enums;

namespace HorseRacing.Domain.Entities;

public sealed class RaceResult
{
    private readonly List<RunnerResult> _runnerResults = [];

    private RaceResult()
    {
    }

    private RaceResult(
        Guid id,
        Guid raceId,
        DateTimeOffset publishedAtUtc,
        ResultStatus status,
        TimeSpan? winningTime)
    {
        Id = id;
        RaceId = raceId;
        PublishedAtUtc = publishedAtUtc.ToUniversalTime();
        Status = status;
        WinningTime = winningTime;
    }

    public Guid Id { get; private set; }

    public Guid RaceId { get; private set; }

    public Race Race { get; private set; } = null!;

    public DateTimeOffset PublishedAtUtc { get; private set; }

    public ResultStatus Status { get; private set; }

    public TimeSpan? WinningTime { get; private set; }

    public IReadOnlyCollection<RunnerResult> RunnerResults => _runnerResults.AsReadOnly();

    internal void AttachTo(Race race) => Race = race;

    internal static RaceResult Create(
        Guid raceId,
        DateTimeOffset publishedAtUtc,
        ResultStatus status,
        TimeSpan? winningTime)
    {
        if (raceId == Guid.Empty)
        {
            throw new ArgumentException("Race identifier is required.", nameof(raceId));
        }

        if (winningTime is { } time && time <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(winningTime), "Winning time must be positive.");
        }

        return new RaceResult(Guid.NewGuid(), raceId, publishedAtUtc, status, winningTime);
    }

    public RunnerResult AddRunnerResult(
        Runner runner,
        RunnerOutcome outcome,
        int? finishPosition,
        bool isDeadHeat = false,
        decimal? distanceBeatenLengths = null,
        decimal? startingPriceDecimal = null,
        decimal? prizeMoney = null)
    {
        ArgumentNullException.ThrowIfNull(runner);

        if (Status != ResultStatus.Provisional)
        {
            throw new InvalidOperationException("An official result cannot be edited in place.");
        }

        if (runner.RaceId != RaceId)
        {
            throw new InvalidOperationException("The runner does not belong to this race.");
        }

        if (runner.Status == Enums.RunnerStatus.NonRunner)
        {
            throw new InvalidOperationException("A non-runner cannot have a race result.");
        }

        if (_runnerResults.Any(result => result.RunnerId == runner.Id))
        {
            throw new InvalidOperationException("A runner can only have one result in a race.");
        }

        if (finishPosition is not null && _runnerResults.Any(
                result => result.FinishPosition == finishPosition &&
                          (!result.IsDeadHeat || !isDeadHeat)))
        {
            throw new InvalidOperationException(
                "A finish position can only be shared when every runner is marked as a dead heat.");
        }

        var runnerResult = RunnerResult.Create(
            Id,
            RaceId,
            runner.Id,
            outcome,
            finishPosition,
            isDeadHeat,
            distanceBeatenLengths,
            startingPriceDecimal,
            prizeMoney);

        _runnerResults.Add(runnerResult);
        return runnerResult;
    }

    public void MakeOfficial(DateTimeOffset publishedAtUtc)
    {
        if (Status == ResultStatus.Official)
        {
            throw new InvalidOperationException("The result is already official.");
        }

        if (publishedAtUtc < PublishedAtUtc)
        {
            throw new ArgumentException("Publication time cannot move backwards.", nameof(publishedAtUtc));
        }

        if (Race is null || Race.Runners.Count == 0 ||
            !_runnerResults.Any(result => result.FinishPosition == 1) ||
            Race.Runners.Where(runner => runner.Status != RunnerStatus.NonRunner)
                .Any(runner => !_runnerResults.Any(result => result.RunnerId == runner.Id)))
        {
            throw new InvalidOperationException(
                "An official result requires a winner and an outcome for every starter; load the full race first.");
        }

        if (_runnerResults.Any(result => result.IsDeadHeat &&
                _runnerResults.Count(other => other.FinishPosition == result.FinishPosition) < 2))
        {
            throw new InvalidOperationException("A dead heat must contain at least two finishers at the same position.");
        }

        Status = ResultStatus.Official;
        PublishedAtUtc = publishedAtUtc.ToUniversalTime();
    }
}
