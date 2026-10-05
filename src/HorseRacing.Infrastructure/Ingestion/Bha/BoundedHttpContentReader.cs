namespace HorseRacing.Infrastructure.Ingestion.Bha;

internal static class BoundedHttpContentReader
{
    public static async Task<byte[]> ReadAsync(
        HttpContent content,
        int maximumResponseBytes,
        string sourceDescription,
        CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new MemoryStream();
        var buffer = new byte[81920];

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (destination.Length + read > maximumResponseBytes)
            {
                throw new InvalidOperationException(
                    $"The {sourceDescription} response exceeded the configured limit of " +
                    $"{maximumResponseBytes} bytes.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return destination.ToArray();
    }
}
