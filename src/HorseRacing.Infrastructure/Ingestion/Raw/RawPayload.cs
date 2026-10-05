using HorseRacing.Application.Ingestion.Raw;

namespace HorseRacing.Infrastructure.Ingestion.Raw;

public sealed class RawPayload
{
    private RawPayload()
    {
    }

    private RawPayload(RawPayloadCapture capture)
    {
        Id = capture.PayloadId;
        CollectionRunId = capture.RunId;
        SourceUrl = capture.SourceUri.AbsoluteUri;
        EffectiveUrl = capture.EffectiveUri.AbsoluteUri;
        RetrievedAtUtc = capture.RetrievedAtUtc;
        HttpStatusCode = capture.HttpStatusCode;
        MediaType = capture.MediaType;
        CharacterEncoding = capture.CharacterEncoding;
        EntityTag = capture.EntityTag;
        LastModifiedUtc = capture.LastModifiedUtc;
        Sha256 = capture.Sha256;
        ContentLength = capture.Content.LongLength;
        Content = capture.Content;
    }

    public Guid Id { get; private set; }

    public Guid CollectionRunId { get; private set; }

    public string SourceUrl { get; private set; } = string.Empty;

    public string EffectiveUrl { get; private set; } = string.Empty;

    public DateTimeOffset RetrievedAtUtc { get; private set; }

    public int HttpStatusCode { get; private set; }

    public string? MediaType { get; private set; }

    public string? CharacterEncoding { get; private set; }

    public string? EntityTag { get; private set; }

    public DateTimeOffset? LastModifiedUtc { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public long ContentLength { get; private set; }

    public byte[] Content { get; private set; } = [];

    public RawCollectionRun CollectionRun { get; private set; } = null!;

    public static RawPayload Create(RawPayloadCapture capture) => new(capture);
}
