using HorseRacing.Domain.Entities;

namespace HorseRacing.Domain.UnitTests;

public sealed class RaceTests
{
    [Fact]
    public void AddRunner_AddsAValidRunner()
    {
        var race = Race.Create(Guid.NewGuid(), "Example Stakes", DateTimeOffset.UtcNow.AddDays(1));
        var horseId = Guid.NewGuid();

        var runner = race.AddRunner(horseId, clothNumber: 4, declaredOdds: 3.5m);

        Assert.Single(race.Runners);
        Assert.Equal(horseId, runner.HorseId);
        Assert.Equal(4, runner.ClothNumber);
    }

    [Fact]
    public void AddRunner_RejectsDuplicateClothNumber()
    {
        var race = Race.Create(Guid.NewGuid(), "Example Stakes", DateTimeOffset.UtcNow.AddDays(1));
        race.AddRunner(Guid.NewGuid(), clothNumber: 4);

        Assert.Throws<InvalidOperationException>(() =>
            race.AddRunner(Guid.NewGuid(), clothNumber: 4));
    }
}
