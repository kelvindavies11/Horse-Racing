namespace HorseRacing.Domain.Entities;

public sealed class Runner
{
    private Runner()
    {
    }

    private Runner(Guid id, Guid raceId, Guid horseId, int clothNumber, decimal? declaredOdds)
    {
        Id = id;
        RaceId = raceId;
        HorseId = horseId;
        ClothNumber = clothNumber;
        DeclaredOdds = declaredOdds;
    }

    public Guid Id { get; private set; }

    public Guid RaceId { get; private set; }

    public Race Race { get; private set; } = null!;

    public Guid HorseId { get; private set; }

    public Horse Horse { get; private set; } = null!;

    public int ClothNumber { get; private set; }

    public decimal? DeclaredOdds { get; private set; }

    internal static Runner Create(Guid raceId, Guid horseId, int clothNumber, decimal? declaredOdds)
    {
        if (raceId == Guid.Empty)
        {
            throw new ArgumentException("Race identifier is required.", nameof(raceId));
        }

        if (horseId == Guid.Empty)
        {
            throw new ArgumentException("Horse identifier is required.", nameof(horseId));
        }

        if (clothNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clothNumber), "Cloth number must be positive.");
        }

        if (declaredOdds is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(declaredOdds), "Declared odds must be positive.");
        }

        return new Runner(Guid.NewGuid(), raceId, horseId, clothNumber, declaredOdds);
    }
}
