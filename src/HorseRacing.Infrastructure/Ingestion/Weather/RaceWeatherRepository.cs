using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HorseRacing.Application.Ingestion.Weather;
using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Infrastructure.Ingestion.Weather;

public sealed partial class RaceWeatherRepository(HorseRacingDbContext dbContext)
    : IRaceWeatherRepository
{
    public async Task<IReadOnlyCollection<RaceWeatherTarget>> GetTargetsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var fromDateText = fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var toDateText = toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var raceRows = await dbContext.CuratedDomainObjects
            .FromSqlInterpolated($"""
                SELECT race.*
                FROM curated.domain_objects AS race
                WHERE race.domain_object_type = {"Race"}
                  AND (race.source_data -> 'foundData' ->> 'raceDate')
                      BETWEEN {fromDateText} AND {toDateText}
                  AND (
                      NOT EXISTS (
                          SELECT 1
                          FROM curated.race_weather AS weather
                          WHERE weather.curated_race_id = race.id)
                      OR EXISTS (
                          SELECT 1
                          FROM curated.race_weather AS weather
                          INNER JOIN curated.racecourse_locations AS location
                              ON location.id = weather.racecourse_location_id
                          WHERE weather.curated_race_id = race.id
                            AND location.location_source = 'Open-Meteo / GeoNames'))
                """)
            .AsNoTracking()
            .Select(item => new CuratedRow(
                item.Id,
                item.SourceSystem,
                item.DomainObjectType,
                item.SourceKey,
                item.DisplayName,
                item.SourceUrl,
                item.RawPayloadId,
                item.RawCollectionRunId,
                item.SourceDataJson))
            .ToListAsync(cancellationToken);
        var races = raceRows
            .Select(row => (Row: row, Race: ParseRace(row)))
            .Where(item => item.Race is not null)
            .Select(item => (item.Row, Race: item.Race!))
            .ToList();
        if (races.Count == 0)
        {
            return [];
        }

        var sourceSystems = races
            .Select(item => item.Row.SourceSystem)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var meetingKeys = races
            .Select(item => $"{item.Race.FixtureYear}:{item.Race.FixtureId}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var meetingRows = await dbContext.CuratedDomainObjects
            .AsNoTracking()
            .Where(item => sourceSystems.Contains(item.SourceSystem)
                && item.DomainObjectType == "Meeting"
                && meetingKeys.Contains(item.SourceKey))
            .Select(item => new CuratedRow(
                item.Id,
                item.SourceSystem,
                item.DomainObjectType,
                item.SourceKey,
                item.DisplayName,
                item.SourceUrl,
                item.RawPayloadId,
                item.RawCollectionRunId,
                item.SourceDataJson))
            .ToListAsync(cancellationToken);
        var meetings = meetingRows
            .Select(ParseMeeting)
            .Where(meeting => meeting is not null)
            .Select(meeting => meeting!)
            .GroupBy(meeting => (meeting.Row.SourceSystem, meeting.FixtureYear, meeting.FixtureId))
            .ToDictionary(group => group.Key, group => group.Last());

        var courseKeys = meetings.Values
            .Select(meeting => meeting.SourceCourseKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var courseRows = await dbContext.CuratedDomainObjects
            .AsNoTracking()
            .Where(item => sourceSystems.Contains(item.SourceSystem)
                && item.DomainObjectType == "Racecourse"
                && courseKeys.Contains(item.SourceKey))
            .Select(item => new CuratedRow(
                item.Id,
                item.SourceSystem,
                item.DomainObjectType,
                item.SourceKey,
                item.DisplayName,
                item.SourceUrl,
                item.RawPayloadId,
                item.RawCollectionRunId,
                item.SourceDataJson))
            .ToListAsync(cancellationToken);

        var courses = courseRows
            .Select(ParseCourse)
            .Where(course => course is not null)
            .Select(course => course!)
            .GroupBy(course => (course.Row.SourceSystem, course.SourceCourseKey))
            .ToDictionary(group => group.Key, group => group.Last());
        var targets = new List<RaceWeatherTarget>();

        foreach (var item in races)
        {
            var row = item.Row;
            var race = item.Race;
            if (!meetings.TryGetValue(
                    (row.SourceSystem, race.FixtureYear, race.FixtureId),
                    out var meeting))
            {
                continue;
            }

            courses.TryGetValue((row.SourceSystem, meeting.SourceCourseKey), out var course);
            var locationLineage = course?.Row ?? meeting.Row;
            targets.Add(new RaceWeatherTarget(
                row.Id,
                row.SourceKey,
                row.DisplayName,
                ToUtc(race.LocalDate, race.LocalTime),
                race.LocalDate,
                race.LocalTime,
                row.SourceSystem,
                meeting.SourceCourseKey,
                meeting.CourseName,
                course?.Postcode,
                course?.Latitude,
                course?.Longitude,
                locationLineage.SourceUrl,
                locationLineage.RawPayloadId,
                locationLineage.RawCollectionRunId));
        }

        return targets
            .OrderBy(target => target.RaceStartUtc)
            .ThenBy(target => target.CourseName)
            .ToList();
    }

    public async Task<StoredRacecourseLocation?> GetLocationAsync(
        string sourceSystem,
        string sourceCourseKey,
        CancellationToken cancellationToken)
    {
        var location = await dbContext.CuratedRacecourseLocations
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.SourceSystem == sourceSystem
                && item.SourceCourseKey == sourceCourseKey,
                cancellationToken);
        return location is null ? null : ToStored(location);
    }

    public async Task<StoredRacecourseLocation?> GetReferenceLocationAsync(
        string courseName,
        CancellationToken cancellationToken)
    {
        var referenceName = NormaliseCourseName(courseName);
        var locations = await dbContext.CuratedRacecourseLocations
            .AsNoTracking()
            .Where(item => item.SourceSystem == "BHA")
            .ToListAsync(cancellationToken);
        var location = locations.FirstOrDefault(item =>
            NormaliseCourseName(item.CourseName) == referenceName);
        return location is null ? null : ToStored(location);
    }

    public async Task<StoredRacecourseLocation> UpsertLocationAsync(
        RacecourseLocationCapture capture,
        CancellationToken cancellationToken)
    {
        var location = await dbContext.CuratedRacecourseLocations.SingleOrDefaultAsync(
            item => item.SourceSystem == capture.SourceSystem
                && item.SourceCourseKey == capture.SourceCourseKey,
            cancellationToken);
        if (location is null)
        {
            location = CuratedRacecourseLocation.Create(capture);
            dbContext.CuratedRacecourseLocations.Add(location);
        }
        else
        {
            location.Apply(capture);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToStored(location);
    }

    public async Task UpsertWeatherAsync(
        IReadOnlyCollection<RaceWeatherCapture> captures,
        CancellationToken cancellationToken)
    {
        if (captures.Count == 0)
        {
            return;
        }

        var raceIds = captures.Select(capture => capture.CuratedRaceId).ToArray();
        var existing = await dbContext.CuratedRaceWeather
            .Where(item => raceIds.Contains(item.CuratedRaceId))
            .ToDictionaryAsync(item => item.CuratedRaceId, cancellationToken);
        foreach (var capture in captures)
        {
            if (existing.TryGetValue(capture.CuratedRaceId, out var weather))
            {
                weather.Apply(capture);
            }
            else
            {
                dbContext.CuratedRaceWeather.Add(CuratedRaceWeather.Create(capture));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static StoredRacecourseLocation ToStored(CuratedRacecourseLocation location) =>
        new(
            location.Id,
            location.SourceSystem,
            location.SourceCourseKey,
            location.CourseName,
            location.Latitude,
            location.Longitude,
            location.TimeZone,
            location.LocationSource,
            location.SourceUrl,
            location.RawPayloadId,
            location.RawCollectionRunId);

    private static string NormaliseCourseName(string value)
    {
        var withoutTags = Regex.Replace(value, @"\s*\([^)]*\)\s*", " ");
        var token = new string(withoutTags
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return token switch
        {
            "catterick" => "catterickbridge",
            "chelmsford" => "chelmsfordcity",
            "epsom" => "epsomdowns",
            "greatyarmouth" => "yarmouth",
            "hamilton" => "hamiltonpark",
            "haydock" => "haydockpark",
            "kempton" => "kemptonpark",
            "lingfield" => "lingfieldpark",
            "sandown" => "sandownpark",
            _ => token
        };
    }

    private static ParsedCourse? ParseCourse(CuratedRow row)
    {
        using var document = JsonDocument.Parse(row.SourceDataJson);
        var data = GetFoundData(document.RootElement);
        var sourceKey = ReadScalar(data, "courseId", "racecourseId", "id") ?? row.SourceKey;
        var latitude = ReadDecimal(data, "latitude", "lat");
        var longitude = ReadDecimal(data, "longitude", "lng", "lon");
        return new ParsedCourse(
            row,
            sourceKey,
            ReadScalar(data, "postcode", "postalCode"),
            latitude,
            longitude);
    }

    private static ParsedMeeting? ParseMeeting(CuratedRow row)
    {
        using var document = JsonDocument.Parse(row.SourceDataJson);
        var data = GetFoundData(document.RootElement);
        var year = ReadInt(data, "fixtureYear");
        var id = ReadInt(data, "fixtureId");
        var courseKey = ReadScalar(data, "courseId", "racecourseId");
        var courseName = ReadScalar(data, "courseName", "racecourseName", "name");
        return year is null || id is null || courseKey is null || courseName is null
            ? null
            : new ParsedMeeting(row, year.Value, id.Value, courseKey, courseName);
    }

    private static ParsedRace? ParseRace(CuratedRow row)
    {
        var match = FixtureRacesUri().Match(row.SourceUrl);
        using var document = JsonDocument.Parse(row.SourceDataJson);
        var data = GetFoundData(document.RootElement);
        var fixtureYear = ReadInt(data, "fixtureYear");
        var fixtureId = ReadInt(data, "fixtureId");
        if (match.Success)
        {
            fixtureYear ??= int.TryParse(match.Groups["year"].Value, out var yearFromUrl)
                ? yearFromUrl
                : null;
            fixtureId ??= int.TryParse(match.Groups["fixture"].Value, out var fixtureFromUrl)
                ? fixtureFromUrl
                : null;
        }
        var dateText = ReadScalar(data, "raceDate");
        var timeText = ReadScalar(data, "raceTime");
        return fixtureYear is not null
            && fixtureId is not null
            && DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && TimeOnly.TryParseExact(timeText, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                ? new ParsedRace(fixtureYear.Value, fixtureId.Value, date, time)
                : null;
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    private static JsonElement GetFoundData(JsonElement root) =>
        TryGet(root, "foundData", out var data) ? data : root;

    private static decimal? ReadDecimal(JsonElement element, params string[] names)
    {
        var value = ReadScalar(element, names);
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static int? ReadInt(JsonElement element, params string[] names)
    {
        var value = ReadScalar(element, names);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static string? ReadScalar(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGet(element, name, out var value)
                && value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                return value.ToString();
            }
        }

        return null;
    }

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

    [GeneratedRegex(@"/bha/v1/fixtures/(?<year>\d{4})/(?<fixture>\d+)/races/?$", RegexOptions.IgnoreCase)]
    private static partial Regex FixtureRacesUri();

    private sealed record CuratedRow(
        Guid Id,
        string SourceSystem,
        string Type,
        string SourceKey,
        string DisplayName,
        string SourceUrl,
        Guid RawPayloadId,
        Guid RawCollectionRunId,
        string SourceDataJson);

    private sealed record ParsedCourse(
        CuratedRow Row,
        string SourceCourseKey,
        string? Postcode,
        decimal? Latitude,
        decimal? Longitude);

    private sealed record ParsedMeeting(
        CuratedRow Row,
        int FixtureYear,
        int FixtureId,
        string SourceCourseKey,
        string CourseName);

    private sealed record ParsedRace(
        int FixtureYear,
        int FixtureId,
        DateOnly LocalDate,
        TimeOnly LocalTime);
}
