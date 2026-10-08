using System.Security.Cryptography;

namespace HorseRacing.Application.Ingestion.Raw;

public sealed class CollectRawSourceHandler(
    IRawSourceClient sourceClient,
    IRawIngestionRepository repository,
    TimeProvider timeProvider)
{
    public async Task<RawCollectionResult> HandleAsync(
        CollectRawSourceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.JobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.SourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CollectorVersion);
        ArgumentNullException.ThrowIfNull(command.SourceUri);

        if (command.MinimumRequestInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The minimum request interval cannot be negative.");
        }

        var runId = Guid.NewGuid();
        var startedAtUtc = timeProvider.GetUtcNow();

        var started = await repository.TryStartAsync(
            new RawCollectionStart(
                runId,
                command.JobName,
                command.SourceName,
                command.SourceUri,
                command.CollectorVersion,
                startedAtUtc,
                command.DispatchItemId),
            command.MinimumRequestInterval,
            cancellationToken);

        if (!started)
        {
            return await WaitForConcurrentCollectionAsync(command, cancellationToken);
        }

        RawSourceResponse response;
        try
        {
            response = await sourceClient.GetAsync(command.SourceUri, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await FinishWithoutPayloadAsync(
                runId,
                RawCollectionOutcome.Cancelled,
                "cancelled",
                "Collection was cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            await FinishWithoutPayloadAsync(
                runId,
                RawCollectionOutcome.Failed,
                "source_request_failed",
                exception.Message);

            return new RawCollectionResult(
                runId,
                RawCollectionOutcome.Failed,
                null,
                null,
                "source_request_failed",
                exception.Message);
        }

        var retrievedAtUtc = timeProvider.GetUtcNow();
        var payloadId = Guid.NewGuid();
        var succeeded = response.StatusCode is >= 200 and <= 299;
        var outcome = succeeded
            ? RawCollectionOutcome.Succeeded
            : RawCollectionOutcome.Failed;
        var errorCode = succeeded ? null : $"http_{response.StatusCode}";
        var errorMessage = succeeded
            ? null
            : response.ReasonPhrase ?? "The source returned a non-success status code.";

        var payload = new RawPayloadCapture(
            payloadId,
            runId,
            command.SourceUri,
            response.EffectiveUri,
            retrievedAtUtc,
            response.StatusCode,
            response.MediaType,
            response.CharacterEncoding,
            response.EntityTag,
            response.LastModifiedUtc,
            Convert.ToHexString(SHA256.HashData(response.Content)).ToLowerInvariant(),
            response.Content);

        await repository.FinishAsync(
            new RawCollectionCompletion(
                runId,
                outcome,
                retrievedAtUtc,
                response.StatusCode,
                payloadId,
                errorCode,
                errorMessage),
            payload,
            cancellationToken);

        return new RawCollectionResult(
            runId,
            outcome,
            payloadId,
            response.StatusCode,
            errorCode,
            errorMessage,
            response.Content);
    }

    private async Task<RawCollectionResult> WaitForConcurrentCollectionAsync(
        CollectRawSourceCommand command,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var latest = await repository.GetLatestAsync(
                command.JobName,
                command.SourceUri,
                cancellationToken);

            if (latest is not null && latest.Outcome != RawCollectionOutcome.Running)
            {
                return latest;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
    }

    private Task FinishWithoutPayloadAsync(
        Guid runId,
        RawCollectionOutcome outcome,
        string errorCode,
        string errorMessage) =>
        repository.FinishAsync(
            new RawCollectionCompletion(
                runId,
                outcome,
                timeProvider.GetUtcNow(),
                null,
                null,
                errorCode,
                errorMessage),
            null,
            CancellationToken.None);
}
