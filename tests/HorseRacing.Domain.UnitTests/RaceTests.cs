using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Enums;

namespace HorseRacing.Domain.UnitTests;

public sealed class RaceTests
{
    [Fact]
    public void AddRunner_AddsAValidRunner()
    {
        var race = CreateRace();
        var horseId = Guid.NewGuid();

        var runner = race.AddRunner(
            horseId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            clothNumber: 4,
            jockeyId: Guid.NewGuid(),
            draw: 2,
            carriedWeightPounds: 126,
            declaredOdds: 3.5m);

        Assert.Single(race.Runners);
        Assert.Equal(horseId, runner.HorseId);
        Assert.Equal(4, runner.ClothNumber);
        Assert.Equal(2, runner.Draw);
        Assert.Equal(RunnerStatus.Declared, runner.Status);
    }

    [Fact]
    public void AddRunner_RejectsDuplicateClothNumber()
    {
        var race = CreateRace();
        AddRunner(race, clothNumber: 4);

        Assert.Throws<InvalidOperationException>(() =>
            AddRunner(race, clothNumber: 4));
    }

    [Fact]
    public void AddRunner_RejectsDuplicateDraw()
    {
        var race = CreateRace();
        AddRunner(race, clothNumber: 1, draw: 3);

        Assert.Throws<InvalidOperationException>(() =>
            AddRunner(race, clothNumber: 2, draw: 3));
    }

    [Fact]
    public void CreateResult_RecordsAValidRunnerResult()
    {
        var race = CreateRace();
        var runner = AddRunner(race, clothNumber: 1);
        race.MarkOff();

        var result = race.CreateResult(
            DateTimeOffset.UtcNow,
            ResultStatus.Provisional,
            TimeSpan.FromSeconds(72.43));
        var runnerResult = result.AddRunnerResult(
            runner,
            RunnerOutcome.Finished,
            finishPosition: 1,
            startingPriceDecimal: 4.5m,
            prizeMoney: 12_500m);

        Assert.Equal(RaceStatus.Finished, race.Status);
        Assert.Single(result.RunnerResults);
        Assert.Equal(1, runnerResult.FinishPosition);
    }

    [Fact]
    public void AddRunnerResult_RejectsANonRunner()
    {
        var race = CreateRace();
        var runner = AddRunner(race, clothNumber: 1);
        runner.MarkNonRunner("Withdrawn by trainer");
        var result = race.CreateResult(DateTimeOffset.UtcNow, ResultStatus.Provisional);

        Assert.Throws<InvalidOperationException>(() =>
            result.AddRunnerResult(runner, RunnerOutcome.Finished, finishPosition: 1));
    }

    private static Race CreateRace()
    {
        return Race.Create(
            Guid.NewGuid(),
            raceNumber: 1,
            "Example Stakes",
            DateTimeOffset.UtcNow.AddDays(1),
            RaceCode.Flat,
            RacingSurface.Turf,
            distanceMetres: 1_609);
    }

    private static Runner AddRunner(Race race, int clothNumber, int? draw = null)
    {
        return race.AddRunner(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            clothNumber,
            jockeyId: Guid.NewGuid(),
            draw: draw);
    }
}
