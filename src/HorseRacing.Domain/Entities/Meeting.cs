using HorseRacing.Domain.Enums;

namespace HorseRacing.Domain.Entities;

public sealed class Meeting
{
    private Meeting()
    {
    }

    private Meeting(
        Guid id,
        Guid racecourseId,
        string name,
        DateOnly scheduledDate,
        MeetingType type)
    {
        Id = id;
        RacecourseId = racecourseId;
        Name = name;
        ScheduledDate = scheduledDate;
        Type = type;
        Status = MeetingStatus.Scheduled;
    }

    public Guid Id { get; private set; }

    public Guid RacecourseId { get; private set; }

    public Racecourse Racecourse { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public DateOnly ScheduledDate { get; private set; }

    public MeetingType Type { get; private set; }

    public MeetingStatus Status { get; private set; }

    public static Meeting Create(
        Guid racecourseId,
        string name,
        DateOnly scheduledDate,
        MeetingType type)
    {
        if (racecourseId == Guid.Empty)
        {
            throw new ArgumentException("Racecourse identifier is required.", nameof(racecourseId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));

        return new Meeting(Guid.NewGuid(), racecourseId, name.Trim(), scheduledDate, type);
    }

    public void MarkInProgress()
    {
        if (Status != MeetingStatus.Scheduled)
        {
            throw new InvalidOperationException("Only a scheduled meeting can start.");
        }

        Status = MeetingStatus.InProgress;
    }

    public void Complete()
    {
        if (Status != MeetingStatus.InProgress)
        {
            throw new InvalidOperationException("Only a meeting in progress can be completed.");
        }

        Status = MeetingStatus.Completed;
    }

    public void Abandon()
    {
        if (Status == MeetingStatus.Completed)
        {
            throw new InvalidOperationException("A completed meeting cannot be abandoned.");
        }

        Status = MeetingStatus.Abandoned;
    }
}
