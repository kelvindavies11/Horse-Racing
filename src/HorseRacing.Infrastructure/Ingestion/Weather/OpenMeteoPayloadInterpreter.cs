using System.Globalization;
using System.Text.Json;
using HorseRacing.Application.Ingestion.Weather;

namespace HorseRacing.Infrastructure.Ingestion.Weather;

public sealed class OpenMeteoPayloadInterpreter : IOpenMeteoPayloadInterpreter
{
    private const string HourlyVariables =
        "temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,rain," +
        "weather_code,wind_speed_10m,wind_direction_10m,wind_gusts_10m";

    public Uri CreateGeocodingUri(string courseName, string? postcode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(courseName);
        var search = string.IsNullOrWhiteSpace(postcode)
            ? $"{courseName}, GB"
            : postcode;
        return new Uri(
            "https://geocoding-api.open-meteo.com/v1/search" +
            $"?name={Uri.EscapeDataString(search)}&count=5&language=en&format=json&countryCode=GB");
    }

    public Uri CreateHistoricalWeatherUri(
        decimal latitude,
        decimal longitude,
        DateOnly localDate,
        string timeZone)
    {
        if (timeZone != "Europe/London")
        {
            throw new ArgumentException("British racing weather must use Europe/London.", nameof(timeZone));
        }

        return new Uri(
            "https://archive-api.open-meteo.com/v1/archive" +
            $"?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&start_date={localDate:yyyy-MM-dd}&end_date={localDate:yyyy-MM-dd}" +
            $"&hourly={HourlyVariables}&timezone=Europe%2FLondon&cell_selection=land");
    }

    public OpenMeteoLocationResult? ReadLocation(byte[] content)
    {
        using var document = JsonDocument.Parse(content);
        if (!TryGet(document.RootElement, "results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in results.EnumerateArray())
        {
            var latitude = ReadDecimal(item, "latitude");
            var longitude = ReadDecimal(item, "longitude");
            var name = ReadString(item, "name");
            if (latitude is null || longitude is null || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var postcode = TryGet(item, "postcodes", out var postcodes)
                           && postcodes.ValueKind == JsonValueKind.Array
                ? postcodes.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                : null;
            return new OpenMeteoLocationResult(
                name,
                postcode,
                latitude.Value,
                longitude.Value,
                ReadString(item, "timezone") ?? "Europe/London");
        }

        return null;
    }

    public OpenMeteoWeatherResult? ReadWeather(
        byte[] content,
        DateTimeOffset raceStartUtc,
        string timeZone)
    {
        using var document = JsonDocument.Parse(content);
        if (!TryGet(document.RootElement, "hourly", out var hourly)
            || !TryGet(hourly, "time", out var times)
            || times.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var localStart = TimeZoneInfo.ConvertTime(raceStartUtc, zone);
        var hourKey = localStart.ToString("yyyy-MM-dd'T'HH:00", CultureInfo.InvariantCulture);
        var timeValues = times.EnumerateArray().Select(item => item.GetString()).ToList();
        var index = timeValues.FindIndex(value => value == hourKey);
        if (index < 0)
        {
            return null;
        }

        var localHour = DateTime.ParseExact(
            hourKey,
            "yyyy-MM-dd'T'HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);
        var weatherHourUtc = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localHour, DateTimeKind.Unspecified), zone));

        return new OpenMeteoWeatherResult(
            weatherHourUtc,
            ReadDecimalAt(hourly, "temperature_2m", index) ?? 0,
            ReadDecimalAt(hourly, "apparent_temperature", index) ?? 0,
            ReadIntAt(hourly, "relative_humidity_2m", index) ?? 0,
            ReadDecimalAt(hourly, "precipitation", index) ?? 0,
            ReadDecimalAt(hourly, "rain", index) ?? 0,
            ReadIntAt(hourly, "weather_code", index) ?? 0,
            ReadDecimalAt(hourly, "wind_speed_10m", index) ?? 0,
            ReadIntAt(hourly, "wind_direction_10m", index) ?? 0,
            ReadDecimalAt(hourly, "wind_gusts_10m", index) ?? 0);
    }

    private static decimal? ReadDecimalAt(JsonElement element, string name, int index) =>
        TryGetArrayItem(element, name, index, out var value) && value.TryGetDecimal(out var number)
            ? number
            : null;

    private static int? ReadIntAt(JsonElement element, string name, int index) =>
        TryGetArrayItem(element, name, index, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;

    private static bool TryGetArrayItem(
        JsonElement element,
        string name,
        int index,
        out JsonElement value)
    {
        if (TryGet(element, name, out var array) && array.ValueKind == JsonValueKind.Array)
        {
            var values = array.EnumerateArray().ToList();
            if (index < values.Count && values[index].ValueKind != JsonValueKind.Null)
            {
                value = values[index];
                return true;
            }
        }

        value = default;
        return false;
    }

    private static decimal? ReadDecimal(JsonElement element, string name) =>
        TryGet(element, name, out var value) && value.TryGetDecimal(out var number)
            ? number
            : null;

    private static string? ReadString(JsonElement element, string name) =>
        TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
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
