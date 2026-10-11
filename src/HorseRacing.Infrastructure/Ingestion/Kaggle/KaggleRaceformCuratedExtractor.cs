using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HorseRacing.Application.Ingestion.Curated;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Kaggle;

public sealed class KaggleRaceformCuratedExtractor(IOptions<KaggleRaceformOptions> options)
{
    public const string SourceSystem = "KaggleRaceform";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly KaggleRaceformOptions _options = options.Value;

    public bool CanExtract(RawPayloadForPromotion payload) =>
        payload.JobName.Equals(_options.RawJobName, StringComparison.Ordinal);

    public CuratedRawPayloadExtraction Extract(RawPayloadForPromotion payload)
    {
        KaggleRaceformRawBatch? batch;
        try
        {
            batch = JsonSerializer.Deserialize<KaggleRaceformRawBatch>(payload.Content, JsonOptions);
        }
        catch (JsonException)
        {
            return CuratedRawPayloadExtraction.Failed(
                "invalid_kaggle_raceform_json",
                "The Kaggle Raceform Raw payload could not be parsed.");
        }

        if (batch is null || batch.SchemaVersion != 1)
        {
            return CuratedRawPayloadExtraction.Failed(
                "unsupported_kaggle_raceform_schema",
                "The Kaggle Raceform Raw payload schema is not supported.");
        }

        var candidates = new Dictionary<string, CuratedDomainObjectCandidate>(StringComparer.Ordinal);
        foreach (var row in batch.Rows)
        {
            if (!TryParseDate(row.Date, out var date)
                || string.IsNullOrWhiteSpace(row.Course))
            {
                continue;
            }

            AddRacecourse(candidates, payload, row);
            AddMeeting(candidates, payload, row, date);
            AddRace(candidates, payload, row, date);
            AddRunnerAndParticipants(candidates, payload, row, date);
        }

        return CuratedRawPayloadExtraction.Succeeded(candidates.Values.ToList());
    }

    private static void AddRacecourse(
        IDictionary<string, CuratedDomainObjectCandidate> candidates,
        RawPayloadForPromotion payload,
        KaggleRaceformRow row)
    {
        var courseKey = EntityKey("course", row.Course);
        Add(candidates, Create(
            payload,
            "Racecourse",
            courseKey,
            row.Course,
            new Dictionary<string, object?>
            {
                ["courseId"] = courseKey,
                ["courseName"] = row.Course,
                ["identityBasis"] = "Course name supplied by the Kaggle Raceform dataset."
            }));
    }

    private static void AddMeeting(
        IDictionary<string, CuratedDomainObjectCandidate> candidates,
        RawPayloadForPromotion payload,
        KaggleRaceformRow row,
        DateOnly date)
    {
        var fixtureId = StablePositiveInt($"meeting|{date:yyyy-MM-dd}|{row.Course}");
        var courseKey = EntityKey("course", row.Course);
        Add(candidates, Create(
            payload,
            "Meeting",
            $"{date.Year}:{fixtureId}",
            $"{row.Course} {date:dd MMM yyyy}",
            new Dictionary<string, object?>
            {
                ["fixtureYear"] = date.Year,
                ["fixtureId"] = fixtureId,
                ["fixtureDate"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["courseId"] = courseKey,
                ["courseName"] = row.Course
            }));
    }

    private static void AddRace(
        IDictionary<string, CuratedDomainObjectCandidate> candidates,
        RawPayloadForPromotion payload,
        KaggleRaceformRow row,
        DateOnly date)
    {
        var fixtureId = StablePositiveInt($"meeting|{date:yyyy-MM-dd}|{row.Course}");
        var raceId = StablePositiveInt($"race|{row.RaceId}");
        var raceName = ValueOr(row.RaceName, $"Race {row.RaceId}");
        Add(candidates, Create(
            payload,
            "Race",
            $"{date.Year}:{raceId}:0",
            raceName,
            new Dictionary<string, object?>
            {
                ["fixtureYear"] = date.Year,
                ["fixtureId"] = fixtureId,
                ["yearOfRace"] = date.Year,
                ["raceId"] = raceId,
                ["sourceRaceId"] = row.RaceId,
                ["divisionSequence"] = 0,
                ["raceDate"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["raceTime"] = NormaliseTime(row.Off),
                ["raceName"] = raceName,
                ["raceCriteriaRaceType"] = ValueOr(row.Type, "Unknown"),
                ["raceClass"] = FirstInteger(row.Class),
                ["pattern"] = row.Pattern,
                ["ratingBand"] = row.RatingBand,
                ["ageBand"] = row.AgeBand,
                ["sexRestriction"] = row.SexRestriction,
                ["distanceText"] = ValueOr(row.Distance, "Distance not recorded"),
                ["goingText"] = ValueOr(row.Going, "Going not recorded"),
                ["prizeAmount"] = row.Prize,
                ["prizeCurrency"] = "GBP",
                ["runnerCount"] = row.Ran,
                ["abandonedReasonCode"] = 0
            }));
    }

    private static void AddRunnerAndParticipants(
        IDictionary<string, CuratedDomainObjectCandidate> candidates,
        RawPayloadForPromotion payload,
        KaggleRaceformRow row,
        DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(row.Horse))
        {
            return;
        }

        var raceId = StablePositiveInt($"race|{row.RaceId}");
        var horseKey = EntityKey("horse", row.Horse);
        var jockeyKey = OptionalEntityKey("jockey", row.Jockey);
        var trainerKey = OptionalEntityKey("trainer", row.Trainer);
        var ownerKey = OptionalEntityKey("owner", row.Owner);
        var runnerKey = $"{date.Year}:{raceId}:0:{horseKey}";

        Add(candidates, Create(
            payload,
            "RunnerResult",
            runnerKey,
            row.Horse,
            new Dictionary<string, object?>
            {
                ["yearOfRace"] = date.Year,
                ["raceId"] = raceId,
                ["sourceRaceId"] = row.RaceId,
                ["divisionSequence"] = 0,
                ["animalId"] = horseKey,
                ["racehorseName"] = row.Horse,
                ["resultFinishPos"] = Positive(row.Position),
                ["clothNumber"] = row.Number,
                ["drawnStall"] = row.Draw,
                ["jockeyId"] = jockeyKey,
                ["jockeyName"] = row.Jockey,
                ["trainerId"] = trainerKey,
                ["trainerName"] = row.Trainer,
                ["ownerId"] = ownerKey,
                ["ownerName"] = row.Owner,
                ["bettingRatio"] = row.StartingPrice,
                ["resultBtnDistance"] = FormatDecimal(row.Beaten ?? row.OverallBeaten),
                ["finishTime"] = row.Time,
                ["status"] = Positive(row.Position) is null ? "Did not finish" : "Result",
                ["DNFReason"] = Positive(row.Position) is null ? row.Comment : null,
                ["weight"] = row.Weight,
                ["headgear"] = row.Headgear,
                ["officialRating"] = row.OfficialRating,
                ["racingPostRating"] = row.RacingPostRating,
                ["topSpeed"] = row.TopSpeed,
                ["comment"] = row.Comment
            }));

        Add(candidates, Create(
            payload,
            "Horse",
            horseKey,
            row.Horse,
            new Dictionary<string, object?>
            {
                ["animalId"] = horseKey,
                ["racehorseName"] = row.Horse,
                ["age"] = row.Age,
                ["sex"] = row.Sex,
                ["sireName"] = row.Sire,
                ["damName"] = row.Dam,
                ["damSireName"] = row.DamSire,
                ["derivedFromResult"] = true
            }));

        AddNamedParticipant(candidates, payload, "Jockey", "jockeyId", "jockeyName", jockeyKey, row.Jockey);
        AddNamedParticipant(candidates, payload, "Trainer", "trainerId", "trainerName", trainerKey, row.Trainer);
        AddNamedParticipant(candidates, payload, "Owner", "ownerId", "ownerName", ownerKey, row.Owner);

        if (trainerKey is not null && !string.IsNullOrWhiteSpace(row.Trainer))
        {
            Add(candidates, Create(
                payload,
                "Stable",
                trainerKey,
                $"Stable of {row.Trainer}",
                new Dictionary<string, object?>
                {
                    ["trainerId"] = trainerKey,
                    ["trainerName"] = row.Trainer,
                    ["stableName"] = $"Stable of {row.Trainer}",
                    ["derivedFromResult"] = true,
                    ["identityBasis"] = "Derived from the result's trainer attribution; the source does not provide an official stable name or location."
                }));
        }
    }

    private static void AddNamedParticipant(
        IDictionary<string, CuratedDomainObjectCandidate> candidates,
        RawPayloadForPromotion payload,
        string type,
        string idProperty,
        string nameProperty,
        string? sourceKey,
        string? displayName)
    {
        if (sourceKey is null || string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        Add(candidates, Create(
            payload,
            type,
            sourceKey,
            displayName,
            new Dictionary<string, object?>
            {
                [idProperty] = sourceKey,
                [nameProperty] = displayName,
                ["derivedFromResult"] = true
            }));
    }

    private static CuratedDomainObjectCandidate Create(
        RawPayloadForPromotion payload,
        string type,
        string sourceKey,
        string displayName,
        IReadOnlyDictionary<string, object?> foundData)
    {
        var sourceData = JsonSerializer.Serialize(
            new
            {
                sourceSystem = SourceSystem,
                domainObjectType = type,
                sourceKey,
                displayName,
                foundData
            },
            JsonOptions);
        return new CuratedDomainObjectCandidate(
            SourceSystem,
            type,
            sourceKey,
            displayName,
            payload.SourceUri,
            payload.RetrievedAtUtc,
            sourceData);
    }

    private static void Add(
        IDictionary<string, CuratedDomainObjectCandidate> candidates,
        CuratedDomainObjectCandidate candidate) =>
        candidates[$"{candidate.DomainObjectType}\u001f{candidate.SourceKey}"] = candidate;

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(
            value,
            ["yyyy-MM-dd", "yyyyMMdd", "dd/MM/yyyy"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    private static string NormaliseTime(string? value)
    {
        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return "00:00:00";
    }

    private static int? FirstInteger(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.SkipWhile(character => !char.IsDigit(character))
            .TakeWhile(char.IsDigit)
            .ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static int? Positive(int? value) => value > 0 ? value : null;

    private static string? FormatDecimal(decimal? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture);

    private static string ValueOr(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? OptionalEntityKey(string prefix, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : EntityKey(prefix, value);

    private static string EntityKey(string prefix, string value) =>
        $"raceform-{prefix}-{Hash(value.Trim().ToUpperInvariant())[..16]}";

    private static int StablePositiveInt(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return (int)(BitConverter.ToUInt32(bytes, 0) % int.MaxValue) + 1;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
