using System.Globalization;
using System.Text.Json;
using HorseRacing.Application.Ingestion.Results;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaResultsPayloadInterpreter : IRaceResultsPayloadInterpreter
{
    private const string BaseUri = "https://api09.horseracing.software/bha/v1";
    private const string FixtureFields =
        "fixtureYear,fixtureId,courseId,courseName,fixtureDate,fixtureType,fixtureSession," +
        "firstRace,numberOfRaces,going,weather,racingTrackType,abandonedReasonCode,highlightTitle";

    public Uri CreateRacecoursesUri() => new($"{BaseUri}/racecourses/");

    public Uri CreateFixturePageUri(int year, int month, int page) =>
        new($"{BaseUri}/fixtures/?resultsAvailable=1&fields={FixtureFields}" +
            $"&year={year:D4}&month={month:D2}&page={page}&per_page=100");

    public Uri CreateFixtureRacesUri(ResultFixtureReference fixture) =>
        new($"{BaseUri}/fixtures/{fixture.FixtureYear:D4}/{fixture.FixtureId}/races");

    public Uri CreateRaceResultsUri(ResultRaceReference race) =>
        new($"{BaseUri}/races/{race.RaceYear:D4}/{race.RaceId}/{race.DivisionSequence}/results");

    public ResultFixturePage ReadFixturePage(byte[] content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var currentPage = ReadInt(root, "current_page") ?? 1;
        var lastPage = ReadInt(root, "last_page") ?? currentPage;
        var fixtures = new List<ResultFixtureReference>();

        if (TryGet(root, "data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var year = ReadInt(item, "fixtureYear");
                var id = ReadInt(item, "fixtureId");
                var dateValue = ReadString(item, "fixtureDate");
                var courseName = ReadString(item, "courseName");
                if (year is null || id is null || courseName is null
                    || !DateOnly.TryParseExact(
                        dateValue,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var date))
                {
                    continue;
                }

                fixtures.Add(new ResultFixtureReference(year.Value, id.Value, date, courseName));
            }
        }

        return new ResultFixturePage(currentPage, lastPage, fixtures);
    }

    public IReadOnlyCollection<ResultRaceReference> ReadRaces(byte[] content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var data = TryGet(root, "data", out var nested) ? nested : root;
        if (data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var races = new List<ResultRaceReference>();
        foreach (var item in data.EnumerateArray())
        {
            var year = ReadInt(item, "yearOfRace");
            var id = ReadInt(item, "raceId");
            var division = ReadInt(item, "divisionSequence") ?? 0;
            var name = ReadString(item, "raceName");
            if (year is not null && id is not null && name is not null)
            {
                races.Add(new ResultRaceReference(year.Value, id.Value, division, name));
            }
        }

        return races;
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : int.TryParse(value.ToString(), CultureInfo.InvariantCulture, out number)
                ? number
                : null;
    }

    private static string? ReadString(JsonElement element, string name) =>
        TryGet(element, name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
