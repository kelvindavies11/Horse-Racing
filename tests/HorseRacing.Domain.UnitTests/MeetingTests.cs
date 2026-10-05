using HorseRacing.Domain.Entities;
using HorseRacing.Domain.Enums;

namespace HorseRacing.Domain.UnitTests;

public sealed class MeetingTests
{
    [Fact]
    public void Meeting_FollowsTheExpectedLifecycle()
    {
        var meeting = Meeting.Create(
            Guid.NewGuid(),
            "Autumn Afternoon Racing",
            new DateOnly(2026, 10, 5),
            MeetingType.Flat);

        meeting.MarkInProgress();
        meeting.Complete();

        Assert.Equal(MeetingStatus.Completed, meeting.Status);
    }

    [Fact]
    public void Meeting_CannotCompleteBeforeItStarts()
    {
        var meeting = Meeting.Create(
            Guid.NewGuid(),
            "Autumn Afternoon Racing",
            new DateOnly(2026, 10, 5),
            MeetingType.Flat);

        Assert.Throws<InvalidOperationException>(meeting.Complete);
    }
}
