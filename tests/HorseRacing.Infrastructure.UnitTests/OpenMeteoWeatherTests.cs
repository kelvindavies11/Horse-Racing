using System.Text;
using HorseRacing.Infrastructure.Ingestion.Weather;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class OpenMeteoWeatherTests
{
    [Theory]
    [InlineData("https://geocoding-api.open-meteo.com/v1/search?name=Ascot%2C%20GB&count=5&language=en&format=json&countryCode=GB")]
    [InlineData("https://archive-api.open-meteo.com/v1/archive?latitude=51.41&longitude=-0.67&start_date=2026-10-05&end_date=2026-10-05&hourly=temperature_2m&timezone=Europe%2FLondon&cell_selection=land")]
    public void Source_validation_accepts_reviewed_open_meteo_endpoints(string candidate)
    {
        OpenMeteoRawSourceClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("http://archive-api.open-meteo.com/v1/archive?latitude=51&longitude=0&start_date=2026-10-05&end_date=2026-10-05&timezone=Europe%2FLondon")]
    [InlineData("https://api.open-meteo.com/v1/forecast?latitude=51&longitude=0")]
    [InlineData("https://geocoding-api.open-meteo.com/v1/search?name=Ascot&countryCode=US")]
    [InlineData("https://archive-api.open-meteo.com/v1/archive?latitude=91&longitude=0&start_date=2026-10-05&end_date=2026-10-05&timezone=Europe%2FLondon")]
    [InlineData("https://archive-api.open-meteo.com/v1/archive?latitude=51&longitude=0&start_date=2026-10-04&end_date=2026-10-05&timezone=Europe%2FLondon")]
    public void Source_validation_rejects_unreviewed_open_meteo_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => OpenMeteoRawSourceClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Fact]
    public void Location_parser_handles_empty_postcode_collection()
    {
        var interpreter = new OpenMeteoPayloadInterpreter();

        var location = interpreter.ReadLocation(Encoding.UTF8.GetBytes(
            """{"results":[{"name":"Ascot","latitude":51.41,"longitude":-0.67,"postcodes":[],"timezone":"Europe/London"}]}"""));

        Assert.NotNull(location);
        Assert.Equal("Ascot", location.Name);
        Assert.Null(location.Postcode);
    }

    [Fact]
    public void Weather_parser_selects_the_local_race_hour_and_converts_it_to_utc()
    {
        var interpreter = new OpenMeteoPayloadInterpreter();
        var content = Encoding.UTF8.GetBytes(
            """
            {"hourly":{
              "time":["2026-10-05T13:00","2026-10-05T14:00"],
              "temperature_2m":[15.1,16.2],
              "apparent_temperature":[14.2,15.3],
              "relative_humidity_2m":[70,65],
              "precipitation":[0.1,0.2],
              "rain":[0.1,0.2],
              "weather_code":[2,3],
              "wind_speed_10m":[11.0,12.5],
              "wind_direction_10m":[220,225],
              "wind_gusts_10m":[20.0,25.0]
            }}
            """);

        // 13:35 UTC is 14:35 BST on 5 October 2026, so the 14:00 local observation is selected.
        var result = interpreter.ReadWeather(
            content,
            new DateTimeOffset(2026, 10, 5, 13, 35, 0, TimeSpan.Zero),
            "Europe/London");

        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero), result.WeatherHourUtc);
        Assert.Equal(16.2m, result.TemperatureC);
        Assert.Equal(65, result.RelativeHumidityPercent);
        Assert.Equal(25.0m, result.WindGustKilometresPerHour);
    }
}
