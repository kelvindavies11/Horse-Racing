using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Enums;

namespace HorseRacing.Domain.UnitTests;

public sealed class ResultIntegrityTests
{
    private static readonly DateTimeOffset Published = new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

    private static Race Race() => HorseRacing.Domain.Entities.Race.Create(
        Guid.NewGuid(), 1, "Test Stakes", Published.AddMinutes(-10),
        RaceCode.Flat, RacingSurface.Turf, 1609);

    private static Runner Runner(Race race, int cloth = 1) => race.AddRunner(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), cloth, stableId: Guid.NewGuid());

    [Fact]
    public void OfficialResult_RequiresAnOutcomeForEveryStarter()
    {
        var race = Race();
        var winner = Runner(race);
        Runner(race, 2);
        var result = race.CreateResult(Published, ResultStatus.Provisional);
        result.AddRunnerResult(winner, RunnerOutcome.Finished, 1);
        Assert.Throws<InvalidOperationException>(() => result.MakeOfficial(Published));
    }

    [Fact]
    public void OfficialResult_AllowsDeadHeatAndNonFinisherAndExcludesNonRunner()
    {
        var race = Race();
        var first = Runner(race);
        var second = Runner(race, 2);
        var fallen = Runner(race, 3);
        Runner(race, 4).MarkNonRunner("Going");
        var result = race.CreateResult(Published, ResultStatus.Provisional);
        result.AddRunnerResult(first, RunnerOutcome.Finished, 1, isDeadHeat: true);
        result.AddRunnerResult(second, RunnerOutcome.Finished, 1, isDeadHeat: true);
        result.AddRunnerResult(fallen, RunnerOutcome.Fell, null);
        result.MakeOfficial(Published);
        Assert.Equal(ResultStatus.Official, result.Status);
        Assert.Throws<InvalidOperationException>(() => result.AddRunnerResult(first, RunnerOutcome.Finished, 1));
    }

    [Fact]
    public void Result_RejectsForeignRunnerAndDuplicateRunner()
    {
        var race = Race();
        var runner = Runner(race);
        var foreign = Runner(Race());
        var result = race.CreateResult(Published, ResultStatus.Provisional);
        Assert.Throws<InvalidOperationException>(() => result.AddRunnerResult(foreign, RunnerOutcome.Finished, 1));
        result.AddRunnerResult(runner, RunnerOutcome.Finished, 1);
        Assert.Throws<InvalidOperationException>(() => result.AddRunnerResult(runner, RunnerOutcome.Finished, 2));
    }

    [Fact]
    public void Result_RejectsSharedPositionWithoutDeadHeat()
    {
        var race = Race();
        var first = Runner(race);
        var second = Runner(race, 2);
        var result = race.CreateResult(Published, ResultStatus.Provisional);
        result.AddRunnerResult(first, RunnerOutcome.Finished, 1);
        Assert.Throws<InvalidOperationException>(() => result.AddRunnerResult(second, RunnerOutcome.Finished, 1));
    }

    [Theory]
    [InlineData(RunnerOutcome.Finished, null)]
    [InlineData(RunnerOutcome.Fell, 1)]
    [InlineData(RunnerOutcome.Finished, 0)]
    [InlineData((RunnerOutcome)999, null)]
    public void Result_RejectsInvalidOutcomePosition(RunnerOutcome outcome, int? position)
    {
        var race = Race();
        var runner = Runner(race);
        var result = race.CreateResult(Published, ResultStatus.Provisional);
        Assert.ThrowsAny<ArgumentException>(() => result.AddRunnerResult(runner, outcome, position));
    }

    [Fact]
    public void Race_RejectsDeclarationChangesAfterStart()
    {
        var race = Race();
        var runner = Runner(race);
        race.MarkOff();
        Assert.Throws<InvalidOperationException>(() => Runner(race, 2));
        Assert.Throws<InvalidOperationException>(() => runner.AssignJockey(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => runner.MarkNonRunner("Late withdrawal"));
    }

    [Fact]
    public void Race_RejectsAbandonedResultAndDirectOfficialResult()
    {
        var race = Race();
        Assert.Throws<ArgumentException>(() => race.CreateResult(Published, ResultStatus.Official));
        race.Abandon();
        Assert.Throws<InvalidOperationException>(() => race.CreateResult(Published, ResultStatus.Provisional));
    }

    [Fact]
    public void Participants_AllowMissingPublicLicenceAndStableDetails()
    {
        var trainer = Trainer.Create(null, "Training Partnership");
        var jockey = Jockey.Create("Example Rider");
        Assert.Null(trainer.StableId);
        Assert.Null(trainer.LicenceNumber);
        Assert.Null(jockey.LicenceNumber);
        Assert.Throws<ArgumentException>(() => Trainer.Create(Guid.Empty, "Invalid"));
    }
}
