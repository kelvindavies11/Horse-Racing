namespace HorseRacing.Application.Ingestion.Raw;

public interface IRawIngestionRepository
{
    Task<RawCollectionResult?> GetLatestSuccessfulAsync(
        string jobName,
        Uri sourceUri,
        CancellationToken cancellationToken);

    Task<RawCollectionResult?> GetLatestAsync(
        string jobName,
        Uri sourceUri,
        CancellationToken cancellationToken);

    Task<bool> TryStartAsync(
        RawCollectionStart collectionRun,
        TimeSpan minimumRequestInterval,
        CancellationToken cancellationToken);

    Task FinishAsync(
        RawCollectionCompletion completion,
        RawPayloadCapture? payload,
        CancellationToken cancellationToken);
}
