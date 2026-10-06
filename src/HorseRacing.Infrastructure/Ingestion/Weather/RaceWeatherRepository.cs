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
        var existingWeatherRaceIds = await dbContext.CuratedRaceWeather
            .AsNoTracking()
            .Select(weather => weather.CuratedRaceId)
            .ToListAsync(cancellationToken);
        var rows = await dbContext.CuratedDomainObjects
            .AsNoTracking()
            .Where(item => item.DomainObjectType == "Race"
                || item.DomainObjectType == "Meeting"
                || item.DomainObjectType == "Racecourse")
            .Select(item => new CuratedRow(
                item.Id,
                item.DomainObjectType,
                item.SourceKey,
                item.DisplayName,
                item.SourceUrl,
                item.RawPayloadId,
                item.RawCollectionRunId,
                item.SourceDataJson))
            .ToListAsync(cancellationToken);

        var courses = rows
            .Where(row => row.Type == "Racecourse")
            .Select(ParseCourse)
            .Where(course => course is not null)
            .Select(course => course!)
            .GroupBy(course => course.SourceCourseKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var meetings = rows
            .Where(row => row.Type == "Meeting")
            .Select(ParseMeeting)
            .Where(meeting => meeting is not null)
            .Select(meeting => meeting!)
            .GroupBy(meeting => (meeting.FixtureYear, meeting.FixtureId))
            .ToDictionary(group => group.Key, group => group.Last());
        var existing = existingWeatherRaceIds.ToHashSet();
        var targets = new List<RaceWeatherTarget>();

        foreach (var row in rows.Where(row => row.Type == "Race" && !existing.Contains(row.Id)))
        {
            var race = ParseRace(row);
            if (race is null
                || race.LocalDate < fromDate
                || race.LocalDate > toDate
                || !meetings.TryGetValue((race.FixtureYear, race.FixtureId), out var meeting))
            {
                continue;
            }

            courses.TryGetValue(meeting.SourceCourseKey, out var course);
            var locationLineage = course?.Row ?? meeting.Row;
            targets.Add(new RaceWeatherTarget(
                row.Id,
                row.SourceKey,
                row.DisplayName,
                ToUtc(race.LocalDate, race.LocalTime),
                race.LocalDate,
                "BHA",
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
        RaceWeatherCapture capture,
        CancellationToken cancellationToken)
    {
        var weather = await dbContext.CuratedRaceWeather.SingleOrDefaultAsync(
            item => item.CuratedRaceId == capture.CuratedRaceId,
            cancellationToken);
        if (weather is null)
        {
            dbContext.CuratedRaceWeather.Add(CuratedRaceWeather.Create(capture));
        }
        else
        {
            weather.Apply(capture);
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
            location.TimeZone);

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
        if (!match.Success)
        {
            return null;
        }

        using var document = JsonDocument.Parse(row.SourceDataJson);
        var data = GetFoundData(document.RootElement);
        var dateText = ReadScalar(data, "raceDate");
        var timeText = ReadScalar(data, "raceTime");
        return int.TryParse(match.Groups["year"].Value, out var fixtureYear)
            && int.TryParse(match.Groups["fixture"].Value, out var fixtureId)
            && DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && TimeOnly.TryParseExact(timeText, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                ? new ParsedRace(fixtureYear, fixtureId, date, time)
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
