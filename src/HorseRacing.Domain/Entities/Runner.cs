namespace HorseRacing.Domain.Entities;

public sealed class Runner
{
    private Runner()
    {
    }

    private Runner(
        Guid id,
        Guid raceId,
        Guid horseId,
        Guid trainerId,
        Guid ownerId,
        Guid? jockeyId,
        int clothNumber,
        int? draw,
        int? carriedWeightPounds,
        decimal? declaredOdds,
        Guid? stableId)
    {
        Id = id;
        RaceId = raceId;
        HorseId = horseId;
        TrainerId = trainerId;
        StableId = stableId;
        OwnerId = ownerId;
        JockeyId = jockeyId;
        ClothNumber = clothNumber;
        Draw = draw;
        CarriedWeightPounds = carriedWeightPounds;
        DeclaredOdds = declaredOdds;
        Status = Enums.RunnerStatus.Declared;
    }

    public Guid Id { get; private set; }

    public Guid RaceId { get; private set; }

    public Race Race { get; private set; } = null!;

    public Guid HorseId { get; private set; }

    public Horse Horse { get; private set; } = null!;

    public Guid TrainerId { get; private set; }

    public Trainer Trainer { get; private set; } = null!;

    public Guid? StableId { get; private set; }

    public Stable? Stable { get; private set; }

    public Guid OwnerId { get; private set; }

    public Owner Owner { get; private set; } = null!;

    public Guid? JockeyId { get; private set; }

    public Jockey? Jockey { get; private set; }

    public int ClothNumber { get; private set; }

    public int? Draw { get; private set; }

    public int? CarriedWeightPounds { get; private set; }

    public decimal? DeclaredOdds { get; private set; }

    public Enums.RunnerStatus Status { get; private set; }

    public string? NonRunnerReason { get; private set; }

    internal static Runner Create(
        Guid raceId,
        Guid horseId,
        Guid trainerId,
        Guid ownerId,
        Guid? jockeyId,
        int clothNumber,
        int? draw,
        int? carriedWeightPounds,
        decimal? declaredOdds,
        Guid? stableId)
    {
        if (raceId == Guid.Empty)
        {
            throw new ArgumentException("Race identifier is required.", nameof(raceId));
        }

        if (horseId == Guid.Empty)
        {
            throw new ArgumentException("Horse identifier is required.", nameof(horseId));
        }

        if (trainerId == Guid.Empty)
        {
            throw new ArgumentException("Trainer identifier is required.", nameof(trainerId));
        }

        if (stableId == Guid.Empty)
        {
            throw new ArgumentException("Stable identifier cannot be empty.", nameof(stableId));
        }

        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Owner identifier is required.", nameof(ownerId));
        }

        if (jockeyId == Guid.Empty)
        {
            throw new ArgumentException("Jockey identifier cannot be empty.", nameof(jockeyId));
        }

        if (clothNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clothNumber), "Cloth number must be positive.");
        }

        if (declaredOdds is <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(declaredOdds), "Decimal odds must exceed one.");
        }

        if (draw is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(draw), "Draw must be positive.");
        }

        if (carriedWeightPounds is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(carriedWeightPounds),
                "Carried weight must be positive.");
        }

        return new Runner(
            Guid.NewGuid(),
            raceId,
            horseId,
            trainerId,
            ownerId,
            jockeyId,
            clothNumber,
            draw,
            carriedWeightPounds,
            declaredOdds,
            stableId);
    }

    internal void AttachTo(Race race) => Race = race;

    private void EnsureScheduled()
    {
        if (Race is null || Race.Status != Enums.RaceStatus.Scheduled)
        {
            throw new InvalidOperationException("Load the owning scheduled race before changing a declaration.");
        }
    }

    public void AssignJockey(Guid jockeyId)
    {
        EnsureScheduled();
        if (jockeyId == Guid.Empty)
        {
            throw new ArgumentException("Jockey identifier is required.", nameof(jockeyId));
        }

        if (Status == Enums.RunnerStatus.NonRunner)
        {
            throw new InvalidOperationException("A jockey cannot be assigned to a non-runner.");
        }

        JockeyId = jockeyId;
    }

    public void MarkNonRunner(string reason)
    {
        EnsureScheduled();
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status == Enums.RunnerStatus.NonRunner)
        {
            throw new InvalidOperationException("The runner is already marked as a non-runner.");
        }

        Status = Enums.RunnerStatus.NonRunner;
        NonRunnerReason = reason.Trim();
    }
}
