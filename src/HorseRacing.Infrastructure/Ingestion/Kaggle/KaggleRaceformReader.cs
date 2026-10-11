using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace HorseRacing.Infrastructure.Ingestion.Kaggle;

public sealed class KaggleRaceformReader(ILogger<KaggleRaceformReader> logger)
{
    public async Task PrepareAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(databasePath, readOnly: false, cancellationToken);
        logger.LogInformation("Preparing the local date index used by monthly bulk reads.");
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE INDEX IF NOT EXISTS ix_data_date ON data(date);";
        command.CommandTimeout = 600;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KaggleRaceformRow>> ReadAsync(
        string databasePath,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentOutOfRangeException(nameof(toDate));
        }

        await using var connection = await OpenAsync(databasePath, readOnly: true, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, course, race_id, off, race_name, type, class, pattern,
                   rating_band, age_band, sex_rest, dist, going, ran, num, pos,
                   draw, ovr_btn, btn, horse, age, sex, wgt, hg, time, sp, jockey,
                   trainer, prize, "or", rpr, ts, sire, dam, damsire, owner, comment
            FROM data
            WHERE date >= $fromDate AND date <= $toDate
            ORDER BY date, course, race_id, pos, num;
            """;
        command.Parameters.AddWithValue(
            "$fromDate",
            fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue(
            "$toDate",
            toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.CommandTimeout = 300;

        var rows = new List<KaggleRaceformRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var date = Text(reader, 0);
            var course = Text(reader, 1);
            var raceId = Integer64(reader, 2);
            if (string.IsNullOrWhiteSpace(date)
                || string.IsNullOrWhiteSpace(course)
                || raceId is null)
            {
                continue;
            }

            rows.Add(new KaggleRaceformRow(
                date,
                course,
                raceId.Value,
                Text(reader, 3),
                Text(reader, 4),
                Text(reader, 5),
                Text(reader, 6),
                Text(reader, 7),
                Text(reader, 8),
                Text(reader, 9),
                Text(reader, 10),
                Text(reader, 11),
                Text(reader, 12),
                Integer(reader, 13),
                Integer(reader, 14),
                Integer(reader, 15),
                Integer(reader, 16),
                Decimal(reader, 17),
                Decimal(reader, 18),
                Text(reader, 19),
                Integer(reader, 20),
                Text(reader, 21),
                Text(reader, 22),
                Text(reader, 23),
                Text(reader, 24),
                Text(reader, 25),
                Text(reader, 26),
                Text(reader, 27),
                Integer64(reader, 28),
                Integer(reader, 29),
                Integer(reader, 30),
                Integer(reader, 31),
                Text(reader, 32),
                Text(reader, 33),
                Text(reader, 34),
                Text(reader, 35),
                Text(reader, 36)));
        }

        return rows;
    }

    private static async Task<SqliteConnection> OpenAsync(
        string databasePath,
        bool readOnly,
        CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string? Text(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim();

    private static int? Integer(SqliteDataReader reader, int ordinal) =>
        int.TryParse(Text(reader, ordinal), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static long? Integer64(SqliteDataReader reader, int ordinal) =>
        long.TryParse(Text(reader, ordinal), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static decimal? Decimal(SqliteDataReader reader, int ordinal) =>
        decimal.TryParse(Text(reader, ordinal), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
