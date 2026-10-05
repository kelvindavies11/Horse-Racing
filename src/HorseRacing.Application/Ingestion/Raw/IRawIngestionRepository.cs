namespace HorseRacing.Application.Ingestion.Raw;

public interface IRawIngestionRepository
{
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
