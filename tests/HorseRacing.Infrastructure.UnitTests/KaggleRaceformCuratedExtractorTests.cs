using System.IO.Compression;
using System.Net;
using System.Text.Json;
using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Kaggle;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class KaggleRaceformCuratedExtractorTests
{
    [Fact]
    public async Task DownloaderAtomicallyCachesAndExtractsTheExpectedEntry()
    {
        const string entryName = "form_2015-present/form_2015-present/raceform.db";
        var directory = Path.Combine(Path.GetTempPath(), $"horse-racing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            byte[] zipBytes;
            await using (var buffer = new MemoryStream())
            {
                using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry(entryName);
                    await using var target = entry.Open();
                    await target.WriteAsync("sqlite-test"u8.ToArray());
                }

                zipBytes = buffer.ToArray();
            }

            using var client = new HttpClient(new StaticResponseHandler(zipBytes));
            var options = Options.Create(new KaggleRaceformOptions
            {
                DatasetSlug = "test/dataset",
                DatasetVersion = 1,
                DownloadUrl = "https://www.kaggle.com/api/v1/datasets/download/test/dataset?datasetVersionNumber=1",
                ArchiveEntry = entryName,
                CacheDirectory = directory
            });
            var downloader = new KaggleRaceformArchiveDownloader(
                client,
                options,
                TimeProvider.System,
                NullLogger<KaggleRaceformArchiveDownloader>.Instance);

            var result = await downloader.GetAsync(CancellationToken.None);

            Assert.True(File.Exists(result.ArchivePath));
            Assert.True(File.Exists(result.DatabasePath));
            Assert.True(File.Exists(result.ArchivePath + ".manifest.json"));
            Assert.Equal("sqlite-test", await File.ReadAllTextAsync(result.DatabasePath));
            Assert.Equal(64, result.ArchiveSha256.Length);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ReaderUsesThePublishedSqliteSchemaAndDateWindow()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"horse-racing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "raceform.db");
        try
        {
            await using (var connection = new SqliteConnection(
                $"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE data (
                        date NUMERIC, course TEXT, race_id INTEGER, off TEXT,
                        race_name TEXT, type TEXT, class TEXT, pattern TEXT,
                        rating_band TEXT, age_band TEXT, sex_rest TEXT, dist TEXT,
                        going TEXT, ran INTEGER, num INTEGER, pos INTEGER,
                        draw INTEGER, ovr_btn NUMERIC, btn NUMERIC, horse TEXT,
                        age INTEGER, sex TEXT, wgt TEXT, hg TEXT, time TEXT,
                        sp TEXT, jockey TEXT, trainer TEXT, prize INTEGER,
                        "or" INTEGER, rpr INTEGER, ts INTEGER, sire TEXT, dam TEXT,
                        damsire TEXT, owner TEXT, comment TEXT);
                    INSERT INTO data (
                        date, course, race_id, off, race_name, type, class, dist,
                        going, ran, num, pos, draw, horse, age, sex, sp, jockey,
                        trainer, prize, "or", owner)
                    VALUES (
                        '2024-05-04', 'Newmarket', 12345, '14:30', 'The Test Stakes',
                        'Flat', 'Class 2', '1m', 'Good', 1, 4, 1, 2, 'Fast Horse',
                        4, 'G', '5/2', 'A Jockey', 'A Trainer', 25000, 98, 'An Owner');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var reader = new KaggleRaceformReader(NullLogger<KaggleRaceformReader>.Instance);
            await reader.PrepareAsync(databasePath, CancellationToken.None);
            var rows = await reader.ReadAsync(
                databasePath,
                new DateOnly(2024, 5, 1),
                new DateOnly(2024, 5, 31),
                CancellationToken.None);

            var row = Assert.Single(rows);
            Assert.Equal("Newmarket", row.Course);
            Assert.Equal(12345, row.RaceId);
            Assert.Equal("Fast Horse", row.Horse);
            Assert.Equal(98, row.OfficialRating);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ExtractCreatesWebCompatibleRaceHierarchyAndParticipants()
    {
        var batch = new KaggleRaceformRawBatch(
            1,
            "test/dataset",
            1,
            "raceform.db",
            new string('a', 64),
            new DateOnly(2024, 5, 1),
            new DateOnly(2024, 5, 31),
            [new KaggleRaceformRow(
                "2024-05-04",
                "Newmarket",
                12345,
                "14:30",
                "The Test Stakes",
                "Flat",
                "Class 2",
                null,
                null,
                null,
                null,
                "1m",
                "Good",
                1,
                4,
                1,
                2,
                0,
                0,
                "Fast Horse",
                4,
                "G",
                "9-2",
                null,
                "1m 36.10s",
                "5/2",
                "A Jockey",
                "A Trainer",
                25000,
                98,
                105,
                94,
                "A Sire",
                "A Dam",
                "A Dam Sire",
                "An Owner",
                "Led throughout")]);
        var payload = new RawPayloadForPromotion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "kaggle-raceform-results",
            "Kaggle Raceform bulk dataset",
            new Uri("https://www.kaggle.com/datasets/test/dataset/versions/1?rawMonth=2024-05"),
            new Uri("https://www.kaggle.com/api/v1/datasets/download/test/dataset"),
            DateTimeOffset.Parse("2024-06-01T00:00:00Z"),
            200,
            "application/json",
            "utf-8",
            new string('b', 64),
            JsonSerializer.SerializeToUtf8Bytes(batch, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var extractor = new KaggleRaceformCuratedExtractor(
            Options.Create(new KaggleRaceformOptions()));

        var result = extractor.Extract(payload);

        Assert.Equal(CuratedPromotionOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.DomainObjects, item => item.DomainObjectType == "Racecourse");
        Assert.Contains(result.DomainObjects, item => item.DomainObjectType == "Meeting");
        Assert.Contains(result.DomainObjects, item => item.DomainObjectType == "Race");
        Assert.Contains(result.DomainObjects, item => item.DomainObjectType == "RunnerResult");
        Assert.Contains(result.DomainObjects, item => item.DomainObjectType == "Horse");
        Assert.Contains(result.DomainObjects, item => item.DomainObjectType == "Jockey");
        Assert.Contains(result.DomainObjects, item => item.DomainObjectType == "Trainer");
        Assert.Contains(result.DomainObjects, item => item.DomainObjectType == "Owner");
        Assert.Contains(result.DomainObjects, item => item.DomainObjectType == "Stable");
        Assert.All(
            result.DomainObjects,
            item => Assert.Equal(KaggleRaceformCuratedExtractor.SourceSystem, item.SourceSystem));

        var race = Assert.Single(result.DomainObjects.Where(item => item.DomainObjectType == "Race"));
        using var document = JsonDocument.Parse(race.SourceDataJson);
        var foundData = document.RootElement.GetProperty("foundData");
        Assert.Equal("2024-05-04", foundData.GetProperty("raceDate").GetString());
        Assert.Equal("14:30:00", foundData.GetProperty("raceTime").GetString());
        Assert.Equal(2, foundData.GetProperty("raceClass").GetInt32());
    }

    private sealed class StaticResponseHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content),
                RequestMessage = request
            });
    }
}
