using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Kaggle;

public sealed class KaggleRaceformArchiveDownloader(
    HttpClient httpClient,
    IOptions<KaggleRaceformOptions> options,
    TimeProvider timeProvider,
    ILogger<KaggleRaceformArchiveDownloader> logger)
{
    private readonly KaggleRaceformOptions _options = options.Value;

    public async Task<KaggleRaceformArchive> GetAsync(CancellationToken cancellationToken)
    {
        var cacheDirectory = Path.GetFullPath(_options.CacheDirectory);
        Directory.CreateDirectory(cacheDirectory);

        var archivePath = Path.Combine(
            cacheDirectory,
            $"raceform-v{_options.DatasetVersion}.zip");
        var databasePath = Path.Combine(
            cacheDirectory,
            $"raceform-v{_options.DatasetVersion}.db");
        var manifestPath = archivePath + ".manifest.json";

        string archiveSha256;
        if (File.Exists(archivePath) && new FileInfo(archivePath).Length > 0)
        {
            logger.LogInformation("Reusing cached bulk archive {ArchivePath}.", archivePath);
            archiveSha256 = await ComputeSha256Async(archivePath, cancellationToken);
        }
        else
        {
            archiveSha256 = await DownloadAsync(archivePath, cancellationToken);
            var manifest = new ArchiveManifest(
                _options.DatasetSlug,
                _options.DatasetVersion,
                new Uri(_options.DownloadUrl).GetLeftPart(UriPartial.Path),
                archiveSha256,
                new FileInfo(archivePath).Length,
                timeProvider.GetUtcNow());
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest),
                cancellationToken);
        }

        if (!File.Exists(databasePath) || new FileInfo(databasePath).Length == 0)
        {
            await ExtractDatabaseAsync(archivePath, databasePath, cancellationToken);
        }
        else
        {
            logger.LogInformation("Reusing extracted race database {DatabasePath}.", databasePath);
        }

        return new KaggleRaceformArchive(
            archivePath,
            databasePath,
            archiveSha256,
            new FileInfo(archivePath).Length);
    }

    private async Task<string> DownloadAsync(
        string archivePath,
        CancellationToken cancellationToken)
    {
        var partialPath = archivePath + ".partial";
        if (File.Exists(partialPath))
        {
            File.Delete(partialPath);
        }

        logger.LogInformation(
            "Downloading Kaggle dataset {DatasetSlug} version {DatasetVersion}.",
            _options.DatasetSlug,
            _options.DatasetVersion);

        using var request = new HttpRequestMessage(HttpMethod.Get, _options.DownloadUrl);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        string sha256;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destination = new FileStream(
            partialPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024];
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                hash.AppendData(buffer, 0, bytesRead);
            }

            await destination.FlushAsync(cancellationToken);
            sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        using (var archive = ZipFile.OpenRead(partialPath))
        {
            if (archive.GetEntry(_options.ArchiveEntry) is not { Length: > 0 })
            {
                throw new InvalidDataException(
                    $"The download is not the expected dataset archive containing '{_options.ArchiveEntry}'.");
            }
        }

        File.Move(partialPath, archivePath, true);
        logger.LogInformation(
            "Cached {ArchiveBytes} bytes with SHA-256 {ArchiveFingerprint}.",
            new FileInfo(archivePath).Length,
            sha256[..12]);
        return sha256;
    }

    private async Task ExtractDatabaseAsync(
        string archivePath,
        string databasePath,
        CancellationToken cancellationToken)
    {
        var partialPath = databasePath + ".partial";
        if (File.Exists(partialPath))
        {
            File.Delete(partialPath);
        }

        logger.LogInformation("Extracting {ArchiveEntry} from the bulk archive.", _options.ArchiveEntry);
        await using var archiveStream = new FileStream(
            archivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, false);
        var entry = archive.GetEntry(_options.ArchiveEntry)
            ?? throw new InvalidDataException(
                $"Archive entry '{_options.ArchiveEntry}' was not found.");
        if (entry.Length <= 0)
        {
            throw new InvalidDataException("The race database archive entry is empty.");
        }

        await using (var source = entry.Open())
        await using (var destination = new FileStream(
            partialPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }

        File.Move(partialPath, databasePath, true);
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record ArchiveManifest(
        string DatasetSlug,
        int DatasetVersion,
        string PublicDownloadEndpoint,
        string Sha256,
        long Bytes,
        DateTimeOffset DownloadedAtUtc);
}
