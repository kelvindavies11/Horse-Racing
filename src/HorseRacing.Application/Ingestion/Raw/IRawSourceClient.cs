namespace HorseRacing.Application.Ingestion.Raw;

public interface IRawSourceClient
{
    Task<RawSourceResponse> GetAsync(Uri sourceUri, CancellationToken cancellationToken);
}
