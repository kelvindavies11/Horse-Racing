using System.Net;
using System.Text;
using HorseRacing.Application.Ingestion.Results;
using HorseRacing.Infrastructure.Ingestion.Bha;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class BhaHistoricalResultsTests
{
    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/racecourses/")]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=1&fields=fixtureId&year=2026&month=10&page=1&per_page=100")]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/2026/123/races")]
    [InlineData("https://api09.horseracing.software/bha/v1/races/2026/456/0/results")]
    public void Source_validation_accepts_reviewed_historical_result_endpoints(string candidate)
    {
        BhaHistoricalResultsApiClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("http://api09.horseracing.software/bha/v1/racecourses/")]
    [InlineData("https://example.com/bha/v1/races/2026/456/0/results")]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/?year=2026&month=10&page=1")]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=1&year=2026&month=13&page=1")]
    [InlineData("https://api09.horseracing.software/bha/v1/races/2026/456/0/results?redirect=https://example.com")]
    public void Source_validation_rejects_unreviewed_historical_result_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaHistoricalResultsApiClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Fact]
    public void Interpreter_reads_fixture_pagination_and_filters_malformed_records()
    {
        var interpreter = new BhaResultsPayloadInterpreter();
        var content = Encoding.UTF8.GetBytes(
            """
            {
              "current_page": 2,
              "last_page": 3,
              "data": [
                {"fixtureYear":2026,"fixtureId":101,"fixtureDate":"2026-10-05","courseName":"Ascot"},
                {"fixtureYear":2026,"fixtureDate":"2026-10-05","courseName":"Missing id"}
              ]
            }
            """);

        var page = interpreter.ReadFixturePage(content);

        Assert.Equal(2, page.CurrentPage);
        Assert.Equal(3, page.LastPage);
        var fixture = Assert.Single(page.Fixtures);
        Assert.Equal(new DateOnly(2026, 10, 5), fixture.FixtureDate);
        Assert.Equal(101, fixture.FixtureId);
        Assert.Equal("Ascot", fixture.CourseName);
    }

    [Fact]
    public void Interpreter_builds_and_reads_race_result_hierarchy()
    {
        var interpreter = new BhaResultsPayloadInterpreter();
        var fixture = new ResultFixtureReference(2026, 101, new DateOnly(2026, 10, 5), "Ascot");
        var races = interpreter.ReadRaces(Encoding.UTF8.GetBytes(
            """{"data":[{"yearOfRace":2026,"raceId":202,"divisionSequence":1,"raceName":"The Example Stakes"}]}"""));

        var race = Assert.Single(races);
        Assert.Equal(
            "https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=1&fields=fixtureYear,fixtureId,courseId,courseName,fixtureDate,fixtureType,fixtureSession,firstRace,numberOfRaces,going,weather,racingTrackType,abandonedReasonCode,highlightTitle&year=2026&month=10&page=1&per_page=250",
            interpreter.CreateFixturePageUri(2026, 10, 1).ToString());
        Assert.Equal("https://api09.horseracing.software/bha/v1/fixtures/2026/101/races", interpreter.CreateFixtureRacesUri(fixture).ToString());
        Assert.Equal("https://api09.horseracing.software/bha/v1/races/2026/202/1/results", interpreter.CreateRaceResultsUri(race).ToString());
        Assert.Equal("The Example Stakes", race.RaceName);
    }

    [Fact]
    public async Task Client_returns_the_first_throttle_response_for_audited_recovery()
    {
        var handler = new RecordingHandler(HttpStatusCode.TooManyRequests);
        using var httpClient = new HttpClient(handler);
        var client = new BhaHistoricalResultsApiClient(
            httpClient,
            Options.Create(new BhaCollectionOptions
            {
                RacingStatusApiBearerToken = "test-token"
            }));

        var response = await client.GetAsync(
            new Uri("https://api09.horseracing.software/bha/v1/races/2026/456/0/results"),
            CancellationToken.None);

        Assert.Equal(429, response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                Content = new StringContent("{\"message\":\"Too Many Attempts.\"}")
            });
        }
    }
}
