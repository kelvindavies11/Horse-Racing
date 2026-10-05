using System.Net;
using System.Text;
using HorseRacing.Infrastructure.Ingestion.Bha;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class BhaRacecardRawClientTests
{
    private static readonly Uri RacecardApiUri =
        new("https://api09.horseracing.software/bha/v1/races/2026/54321/0/entries");

    [Fact]
    public async Task Racecard_api_requires_operator_supplied_bearer_token()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new BhaRacecardApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                RacecardApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = RacecardApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync(RacecardApiUri, CancellationToken.None));

        Assert.Contains("bearer token", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Racecard_api_sends_configured_token_to_reviewed_endpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            ReasonPhrase = "OK",
            Content = new StringContent(
                "{\"data\":[{\"horseName\":\"Example\"}]}",
                Encoding.UTF8,
                "application/json")
        });
        var client = new BhaRacecardApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                RacecardApiBearerToken = "racecard-token",
                RacecardApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = RacecardApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var response = await client.GetAsync(RacecardApiUri, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.MediaType);
        Assert.Equal(
            "{\"data\":[{\"horseName\":\"Example\"}]}",
            Encoding.UTF8.GetString(response.Content));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(RacecardApiUri, request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("racecard-token", request.Headers.Authorization?.Parameter);
        Assert.Equal(
            new Uri("https://www.britishhorseracing.com/racing/fixtures/upcoming/racecard/race/"),
            request.Headers.Referrer);
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/2026/12345/races")]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/2026/12345/going")]
    [InlineData("https://api09.horseracing.software/bha/v1/races/2026/54321/0")]
    [InlineData("https://api09.horseracing.software/bha/v1/races/2026/54321/0/entries")]
    [InlineData("https://api09.horseracing.software/bha/v1/races/2026/54321/0/results")]
    public void Racecard_api_validation_accepts_reviewed_locations(string candidate)
    {
        BhaRacecardApiClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/races/1999/54321/0")]
    [InlineData("https://api09.horseracing.software/bha/v1/races/2026/54321/-1")]
    [InlineData("https://api09.horseracing.software/bha/v1/races/2026/54321/0/odds")]
    [InlineData("https://api09.horseracing.software/bha/v1/races/2026/54321/0/entries?x=1")]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/2026/12345")]
    [InlineData("https://www.britishhorseracing.com/racing/fixtures/upcoming/racecard/race/")]
    public void Racecard_api_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaRacecardApiClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/fixtures/upcoming/?racecourse=Ascot")]
    [InlineData("https://www.britishhorseracing.com/racing/fixtures/full-year/")]
    [InlineData("http://www.britishhorseracing.com/racing/fixtures/upcoming/")]
    public void Upcoming_fixtures_page_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaUpcomingFixturesPageClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/fixtures/upcoming/racecard/race/?race=1")]
    [InlineData("https://www.britishhorseracing.com/racing/fixtures/upcoming/")]
    [InlineData("http://www.britishhorseracing.com/racing/fixtures/upcoming/racecard/race/")]
    public void Racecard_page_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaRacecardPageClient.ValidateSourceUri(new Uri(candidate)));
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responseFactory(request));
        }
    }
}
