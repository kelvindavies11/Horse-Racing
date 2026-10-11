using System.Security.Cryptography;

namespace HorseRacing.Application.Ingestion.Raw;

public sealed class CaptureRawPayloadHandler(
    IRawIngestionRepository repository,
    TimeProvider timeProvider)
{
    public async Task<RawCollectionResult> HandleAsync(
        CaptureRawPayloadCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.JobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.SourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CollectorVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.MediaType);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CharacterEncoding);
        ArgumentNullException.ThrowIfNull(command.SourceUri);
        ArgumentNullException.ThrowIfNull(command.EffectiveUri);
        ArgumentNullException.ThrowIfNull(command.Content);

        if (command.ReuseSuccessful)
        {
            var existing = await repository.GetLatestSuccessfulAsync(
                command.JobName,
                command.SourceUri,
                cancellationToken);
            if (existing is not null)
            {
                return existing with { Content = null };
            }
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
            TimeSpan.Zero,
            cancellationToken);

        if (!started)
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

        var payloadId = Guid.NewGuid();
        var completedAtUtc = timeProvider.GetUtcNow();
        var payload = new RawPayloadCapture(
            payloadId,
            runId,
            command.SourceUri,
            command.EffectiveUri,
            completedAtUtc,
            200,
            command.MediaType,
            command.CharacterEncoding,
            null,
            null,
            Convert.ToHexString(SHA256.HashData(command.Content)).ToLowerInvariant(),
            command.Content);

        await repository.FinishAsync(
            new RawCollectionCompletion(
                runId,
                RawCollectionOutcome.Succeeded,
                completedAtUtc,
                200,
                payloadId,
                null,
                null),
            payload,
            cancellationToken);

        return new RawCollectionResult(
            runId,
            RawCollectionOutcome.Succeeded,
            payloadId,
            200,
            null,
            null,
            command.Content);
    }
}
