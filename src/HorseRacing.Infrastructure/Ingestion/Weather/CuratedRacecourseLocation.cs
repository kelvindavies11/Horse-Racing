using HorseRacing.Application.Ingestion.Weather;
using HorseRacing.Infrastructure.Ingestion.Raw;

namespace HorseRacing.Infrastructure.Ingestion.Weather;

public sealed class CuratedRacecourseLocation
{
    private CuratedRacecourseLocation()
    {
    }

    private CuratedRacecourseLocation(RacecourseLocationCapture capture)
    {
        Id = Guid.NewGuid();
        SourceSystem = capture.SourceSystem;
        SourceCourseKey = capture.SourceCourseKey;
        Apply(capture);
    }

    public Guid Id { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string SourceCourseKey { get; private set; } = string.Empty;
    public string CourseName { get; private set; } = string.Empty;
    public string? Postcode { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public string TimeZone { get; private set; } = string.Empty;
    public string LocationSource { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;
    public Guid RawPayloadId { get; private set; }
    public Guid RawCollectionRunId { get; private set; }
    public DateTimeOffset ResolvedAtUtc { get; private set; }
    public RawPayload RawPayload { get; private set; } = null!;
    public RawCollectionRun RawCollectionRun { get; private set; } = null!;

    public static CuratedRacecourseLocation Create(RacecourseLocationCapture capture) => new(capture);

    public void Apply(RacecourseLocationCapture capture)
    {
        if (SourceSystem != capture.SourceSystem || SourceCourseKey != capture.SourceCourseKey)
        {
            throw new InvalidOperationException("The racecourse location identity cannot be changed.");
        }

        CourseName = capture.CourseName;
        Postcode = capture.Postcode;
        Latitude = capture.Latitude;
        Longitude = capture.Longitude;
        TimeZone = capture.TimeZone;
        LocationSource = capture.LocationSource;
        SourceUrl = capture.SourceUri.AbsoluteUri;
        RawPayloadId = capture.RawPayloadId;
        RawCollectionRunId = capture.RawCollectionRunId;
        ResolvedAtUtc = capture.ResolvedAtUtc;
    }
}
