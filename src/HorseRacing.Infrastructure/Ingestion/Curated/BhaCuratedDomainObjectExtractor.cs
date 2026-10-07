using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HorseRacing.Application.Ingestion.Curated;

namespace HorseRacing.Infrastructure.Ingestion.Curated;

public sealed class BhaCuratedDomainObjectExtractor : ICuratedRawPayloadExtractor
{
    private static readonly JsonSerializerOptions SourceDataJsonOptions = new()
    {
        WriteIndented = false
    };

    public CuratedRawPayloadExtraction Extract(RawPayloadForPromotion payload)
    {
        var domainObjectType = InferDomainObjectType(payload);
        if (domainObjectType is null)
        {
            return CuratedRawPayloadExtraction.Skipped(
                "unsupported_source",
                "The raw payload source is not mapped to a curated domain object type.");
        }

        if (!IsLikelyJson(payload))
        {
            return CuratedRawPayloadExtraction.Skipped(
                "unsupported_media_type",
                $"The {payload.MediaType ?? "unknown"} payload is not a supported JSON payload.");
        }

        using var document = ParseJson(payload);
        if (document is null)
        {
            return CuratedRawPayloadExtraction.Failed(
                "invalid_json",
                "The raw payload could not be parsed as JSON.");
        }

        var records = FindRecordElements(document.RootElement);
        var candidates = records
            .SelectMany(record => CreateCandidates(payload, domainObjectType, record))
            .ToList();

        return CuratedRawPayloadExtraction.Succeeded(candidates);
    }

    private static JsonDocument? ParseJson(RawPayloadForPromotion payload)
    {
        try
        {
            return JsonDocument.Parse(Decode(payload));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static string Decode(RawPayloadForPromotion payload)
    {
        var encoding = TryGetEncoding(payload.CharacterEncoding) ?? Encoding.UTF8;
        return encoding.GetString(payload.Content);
    }

    private static Encoding? TryGetEncoding(string? characterEncoding)
    {
        if (string.IsNullOrWhiteSpace(characterEncoding))
        {
            return null;
        }

        try
        {
            return Encoding.GetEncoding(characterEncoding);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsLikelyJson(RawPayloadForPromotion payload)
    {
        if (payload.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        foreach (var candidate in payload.Content)
        {
            if (candidate is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return candidate is (byte)'{' or (byte)'[';
        }

        return false;
    }

    private static string? InferDomainObjectType(RawPayloadForPromotion payload)
    {
        var jobName = payload.JobName.ToLowerInvariant();
        var sourceUrl = payload.SourceUri.AbsoluteUri.ToLowerInvariant();

        if (jobName.Contains("racecourses", StringComparison.Ordinal))
        {
            return "Racecourse";
        }

        if (jobName.Contains("results-runners", StringComparison.Ordinal))
        {
            return "RunnerResult";
        }

        if (jobName.Contains("results-races", StringComparison.Ordinal))
        {
            return "Race";
        }

        if (jobName.Contains("results-fixtures", StringComparison.Ordinal))
        {
            return "Meeting";
        }

        if (jobName.Contains("racehorse", StringComparison.Ordinal))
        {
            return "Horse";
        }

        if (jobName.Contains("jockey", StringComparison.Ordinal))
        {
            return "Jockey";
        }

        if (jobName.Contains("trainer", StringComparison.Ordinal)
            && jobName.Contains("non-runner", StringComparison.Ordinal))
        {
            return "Runner";
        }

        if (jobName.Contains("trainer", StringComparison.Ordinal))
        {
            return "Trainer";
        }

        if (jobName.Contains("owner", StringComparison.Ordinal))
        {
            return "Owner";
        }

        if (jobName.Contains("stewards", StringComparison.Ordinal))
        {
            return "StewardReport";
        }

        if (jobName.Contains("results", StringComparison.Ordinal)
            || sourceUrl.Contains("/results", StringComparison.Ordinal))
        {
            return "RaceResult";
        }

        if (sourceUrl.Contains("/entries", StringComparison.Ordinal)
            || sourceUrl.Contains("/balloted", StringComparison.Ordinal)
            || sourceUrl.Contains("/nominations", StringComparison.Ordinal))
        {
            return "Runner";
        }

        if (sourceUrl.Contains("/going", StringComparison.Ordinal))
        {
            return "RaceGoing";
        }

        if (jobName.Contains("racecard", StringComparison.Ordinal)
            || sourceUrl.Contains("/races", StringComparison.Ordinal))
        {
            return "Race";
        }

        if (jobName.Contains("fixtures", StringComparison.Ordinal))
        {
            return "Meeting";
        }

        return null;
    }

    private static IReadOnlyList<JsonElement> FindRecordElements(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .ToList();
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var records = new List<JsonElement>();
        AddKnownCollectionElements(root, records, depth: 0);

        return records.Count > 0
            ? records
            : [root];
    }

    private static void AddKnownCollectionElements(
        JsonElement element,
        List<JsonElement> records,
        int depth)
    {
        if (depth > 3 || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!IsKnownCollectionProperty(property.Name))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                records.AddRange(
                    property.Value
                        .EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.Object));
            }
            else if (property.Value.ValueKind == JsonValueKind.Object)
            {
                AddKnownCollectionElements(property.Value, records, depth + 1);
            }
        }

        if (records.Count > 0)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                AddKnownCollectionElements(property.Value, records, depth + 1);
            }
        }
    }

    private static bool IsKnownCollectionProperty(string propertyName) =>
        propertyName.Equals("data", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("items", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("results", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("racecourses", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("fixtures", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("races", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("entries", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("horses", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("racehorses", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("jockeys", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("trainers", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("owners", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("reports", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("nonRunners", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("nominations", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("balloted", StringComparison.OrdinalIgnoreCase);

    private static CuratedDomainObjectCandidate? TryCreateCandidate(
        RawPayloadForPromotion payload,
        string domainObjectType,
        JsonElement record)
    {
        if (record.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var sourceKey = BoundSourceKey(
            GetCompositeSourceKey(record, domainObjectType)
            ?? GetFirstScalar(record, GetSourceKeyFields(domainObjectType))
            ?? CreateRecordHash(record));
        var displayName = Truncate(
            GetFirstScalar(record, GetDisplayNameFields(domainObjectType))
            ?? sourceKey,
            500);

        return new CuratedDomainObjectCandidate(
            "BHA",
            domainObjectType,
            sourceKey,
            displayName,
            payload.SourceUri,
            payload.RetrievedAtUtc,
            BuildSourceDataJson(domainObjectType, sourceKey, displayName, record));
    }

    private static IEnumerable<CuratedDomainObjectCandidate> CreateCandidates(
        RawPayloadForPromotion payload,
        string domainObjectType,
        JsonElement record)
    {
        var primaryCandidate = TryCreateCandidate(payload, domainObjectType, record);
        if (primaryCandidate is not null)
        {
            yield return primaryCandidate;
        }

        if (domainObjectType != "RunnerResult")
        {
            yield break;
        }

        var horse = TryCreateRelatedCandidate(
            payload,
            record,
            "Horse",
            ["animalId", "horseId", "racehorseId"],
            ["racehorseName", "horseName"],
            ["animalId", "racehorseName"]);
        if (horse is not null)
        {
            yield return horse;
        }

        var jockey = TryCreateRelatedCandidate(
            payload,
            record,
            "Jockey",
            ["jockeyId"],
            ["jockeyName"],
            ["jockeyId", "jockeyName", "jockeyLicenceType"]);
        if (jockey is not null)
        {
            yield return jockey;
        }

        var trainer = TryCreateRelatedCandidate(
            payload,
            record,
            "Trainer",
            ["trainerId"],
            ["trainerName"],
            ["trainerId", "trainerName"]);
        if (trainer is not null)
        {
            yield return trainer;
        }

        var owner = TryCreateRelatedCandidate(
            payload,
            record,
            "Owner",
            ["ownerId"],
            ["ownerName"],
            ["ownerId", "ownerName"]);
        if (owner is not null)
        {
            yield return owner;
        }

        var stable = TryCreateTrainerStableCandidate(payload, record);
        if (stable is not null)
        {
            yield return stable;
        }
    }

    private static CuratedDomainObjectCandidate? TryCreateRelatedCandidate(
        RawPayloadForPromotion payload,
        JsonElement record,
        string domainObjectType,
        string[] sourceKeyFields,
        string[] displayNameFields,
        string[] projectedFields)
    {
        var sourceKeyValue = GetFirstScalar(record, sourceKeyFields);
        var displayNameValue = GetFirstScalar(record, displayNameFields);
        if (string.IsNullOrWhiteSpace(sourceKeyValue) || string.IsNullOrWhiteSpace(displayNameValue))
        {
            return null;
        }

        var sourceKey = BoundSourceKey(sourceKeyValue);
        var displayName = Truncate(displayNameValue, 500);
        var foundData = ProjectFoundData(record, projectedFields);
        foundData["derivedFromResult"] = true;

        return new CuratedDomainObjectCandidate(
            "BHA",
            domainObjectType,
            sourceKey,
            displayName,
            payload.SourceUri,
            payload.RetrievedAtUtc,
            BuildSourceDataJson(domainObjectType, sourceKey, displayName, foundData));
    }

    private static CuratedDomainObjectCandidate? TryCreateTrainerStableCandidate(
        RawPayloadForPromotion payload,
        JsonElement record)
    {
        var trainerId = GetFirstScalar(record, ["trainerId"]);
        var trainerName = GetFirstScalar(record, ["trainerName"]);
        if (string.IsNullOrWhiteSpace(trainerId) || string.IsNullOrWhiteSpace(trainerName))
        {
            return null;
        }

        var sourceKey = BoundSourceKey(trainerId);
        var displayName = Truncate($"Stable of {trainerName}", 500);
        var foundData = ProjectFoundData(record, ["trainerId", "trainerName"]);
        foundData["stableName"] = displayName;
        foundData["derivedFromResult"] = true;
        foundData["identityBasis"] =
            "Derived from the result's trainer attribution; the BHA result does not provide an official stable name or location.";

        return new CuratedDomainObjectCandidate(
            "BHA",
            "Stable",
            sourceKey,
            displayName,
            payload.SourceUri,
            payload.RetrievedAtUtc,
            BuildSourceDataJson("Stable", sourceKey, displayName, foundData));
    }

    private static JsonObject ProjectFoundData(JsonElement record, IEnumerable<string> propertyNames)
    {
        var foundData = new JsonObject();
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(record, propertyName, out var value))
            {
                foundData[propertyName] = JsonNode.Parse(value.GetRawText());
            }
        }

        return foundData;
    }

    private static string[] GetSourceKeyFields(string domainObjectType) =>
        domainObjectType switch
        {
            "Racecourse" => ["racecourseId", "courseId", "racecourseCode", "courseCode", "id", "name", "courseName"],
            "Horse" => ["animalId", "horseId", "racehorseId", "id", "racehorseName", "horseName", "name"],
            "Jockey" => ["jockeyId", "entryId", "id", "jockeyName", "name", "entryName"],
            "Trainer" => ["trainerId", "entryId", "id", "trainerName", "name", "entryName"],
            "Owner" => ["ownerId", "entryId", "id", "ownerName", "name", "entryName"],
            "Meeting" => ["fixtureId", "fixtureKey", "id", "courseId", "courseName", "fixtureDate"],
            "Race" => ["raceId", "raceKey", "divisionSequence", "id", "raceName", "name"],
            "Runner" => ["runnerId", "entryId", "horseId", "racehorseId", "id", "horseName", "name"],
            "RunnerResult" => ["animalId", "runnerId", "entryId", "horseId", "racehorseId", "id", "horseName", "racehorseName", "name"],
            "RaceResult" => ["resultId", "raceId", "fixtureId", "id", "raceName", "courseName"],
            "RaceGoing" => ["fixtureId", "raceId", "id", "courseName"],
            "StewardReport" => ["reportId", "raceId", "fixtureId", "id", "title", "raceName"],
            _ => ["id", "name"]
        };

    private static string[] GetDisplayNameFields(string domainObjectType) =>
        domainObjectType switch
        {
            "Racecourse" => ["courseName", "racecourseName", "name"],
            "Horse" => ["racehorseName", "horseName", "name"],
            "Jockey" => ["jockeyName", "name", "entryName"],
            "Trainer" => ["trainerName", "name", "entryName"],
            "Owner" => ["ownerName", "displayName", "name", "entryName"],
            "Meeting" => ["fixtureName", "meetingName", "courseName", "name"],
            "Race" => ["raceName", "name", "title"],
            "Runner" => ["horseName", "name"],
            "RunnerResult" => ["racehorseName", "horseName", "name"],
            "RaceResult" => ["raceName", "courseName", "name", "title"],
            "RaceGoing" => ["courseName", "raceName", "name"],
            "StewardReport" => ["title", "raceName", "courseName", "name"],
            _ => ["displayName", "name", "title"]
        };

    private static string? GetCompositeSourceKey(JsonElement record, string domainObjectType)
    {
        string?[] parts = domainObjectType switch
        {
            "Meeting" =>
            [
                GetFirstScalar(record, ["fixtureYear"]),
                GetFirstScalar(record, ["fixtureId"])
            ],
            "Race" =>
            [
                GetFirstScalar(record, ["yearOfRace", "raceYear"]),
                GetFirstScalar(record, ["raceId"]),
                GetFirstScalar(record, ["divisionSequence"])
            ],
            "RunnerResult" =>
            [
                GetFirstScalar(record, ["yearOfRace", "raceYear"]),
                GetFirstScalar(record, ["raceId"]),
                GetFirstScalar(record, ["divisionSequence"]),
                GetFirstScalar(record, ["animalId", "runnerId", "horseId", "racehorseId"])
            ],
            _ => []
        };

        return parts.Length > 0 && parts.All(part => !string.IsNullOrWhiteSpace(part))
            ? string.Join(':', parts)
            : null;
    }

    private static string? GetFirstScalar(JsonElement element, IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out var propertyValue))
            {
                continue;
            }

            var value = GetScalar(propertyValue);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetScalar(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };

    private static string BuildSourceDataJson(
        string domainObjectType,
        string sourceKey,
        string displayName,
        JsonElement record)
    {
        var foundData = JsonNode.Parse(record.GetRawText()) ?? new JsonObject();
        return BuildSourceDataJson(domainObjectType, sourceKey, displayName, foundData);
    }

    private static string BuildSourceDataJson(
        string domainObjectType,
        string sourceKey,
        string displayName,
        JsonNode foundData)
    {
        var wrapper = new JsonObject
        {
            ["sourceSystem"] = "BHA",
            ["domainObjectType"] = domainObjectType,
            ["sourceKey"] = sourceKey,
            ["displayName"] = displayName,
            ["foundData"] = foundData
        };

        return wrapper.ToJsonString(SourceDataJsonOptions);
    }

    private static string CreateRecordHash(JsonElement record) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record.GetRawText()))).ToLowerInvariant()}";

    private static string BoundSourceKey(string value)
    {
        if (value.Length <= 300)
        {
            return value;
        }

        return CreateStringHash(value);
    }

    private static string CreateStringHash(string value) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
