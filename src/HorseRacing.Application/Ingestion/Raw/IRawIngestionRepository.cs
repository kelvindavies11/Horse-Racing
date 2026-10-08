namespace HorseRacing.Application.Ingestion.Raw;

public interface IRawIngestionRepository
{
    Task<RawCollectionResult?> GetLatestSuccessfulAsync(
        string jobName,
        Uri sourceUri,
        CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetLatestStartAsync(
        Uri sourceUri,
        CancellationToken cancellationToken);

    Task StartAsync(
        RawCollectionStart collectionRun,
        CancellationToken cancellationToken);

    Task FinishAsync(
        RawCollectionCompletion completion,
        RawPayloadCapture? payload,
        CancellationToken cancellationToken);
}
