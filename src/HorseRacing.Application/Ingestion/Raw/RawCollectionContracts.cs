namespace HorseRacing.Application.Ingestion.Raw;

public sealed record CollectRawSourceCommand(
    string JobName,
    string SourceName,
    Uri SourceUri,
    string CollectorVersion,
    TimeSpan MinimumRequestInterval,
    string? DispatchItemId = null);

public sealed record RawCollectionStart(
    Guid RunId,
    string JobName,
    string SourceName,
    Uri SourceUri,
    string CollectorVersion,
    DateTimeOffset StartedAtUtc,
    string? DispatchItemId = null);

public sealed record RawSourceResponse(
    Uri EffectiveUri,
    int StatusCode,
    string? ReasonPhrase,
    string? MediaType,
    string? CharacterEncoding,
    string? EntityTag,
    DateTimeOffset? LastModifiedUtc,
    byte[] Content);

public sealed record RawPayloadCapture(
    Guid PayloadId,
    Guid RunId,
    Uri SourceUri,
    Uri EffectiveUri,
    DateTimeOffset RetrievedAtUtc,
    int HttpStatusCode,
    string? MediaType,
    string? CharacterEncoding,
    string? EntityTag,
    DateTimeOffset? LastModifiedUtc,
    string Sha256,
    byte[] Content);

public sealed record RawCollectionCompletion(
    Guid RunId,
    RawCollectionOutcome Outcome,
    DateTimeOffset CompletedAtUtc,
    int? HttpStatusCode,
    Guid? PayloadId,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record RawCollectionResult(
    Guid RunId,
    RawCollectionOutcome Outcome,
    Guid? PayloadId,
    int? HttpStatusCode,
    string? ErrorCode,
    string? ErrorMessage,
    byte[]? Content = null);
