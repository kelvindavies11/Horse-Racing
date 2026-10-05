using System.Net;
using System.Text;
using HorseRacing.Infrastructure.Ingestion.Bha;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class BhaRacingStatusRawClientTests
{
    private static readonly Uri ResultsApiUri =
        new("https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=1&fields=courseId,courseName");

    private static readonly Uri StewardsReportsApiUri =
        new("https://api09.horseracing.software/bha/v1/stewards-room/reports");

    [Fact]
    public async Task Racing_status_api_requires_operator_supplied_bearer_token()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new BhaRacingStatusApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                RacingStatusApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = ResultsApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync(ResultsApiUri, CancellationToken.None));

        Assert.Contains("bearer token", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Racing_status_api_sends_configured_token_to_reviewed_endpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            ReasonPhrase = "OK",
            Content = new StringContent(
                "{\"data\":[{\"courseName\":\"Ascot\"}]}",
                Encoding.UTF8,
                "application/json")
        });
        var client = new BhaRacingStatusApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                RacingStatusApiBearerToken = "status-token",
                RacingStatusApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = ResultsApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var response = await client.GetAsync(ResultsApiUri, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.MediaType);
        Assert.Equal(
            "{\"data\":[{\"courseName\":\"Ascot\"}]}",
            Encoding.UTF8.GetString(response.Content));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(ResultsApiUri, request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("status-token", request.Headers.Authorization?.Parameter);
        Assert.Equal(
            new Uri("https://www.britishhorseracing.com/racing/results/"),
            request.Headers.Referrer);
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=1")]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=1&year=2026&month=10&page=2")]
    [InlineData("https://api09.horseracing.software/bha/v1/stewards-room/reports")]
    [InlineData("https://api09.horseracing.software/bha/v1/stewards-room/reports?ondate=2026-10-05")]
    public void Racing_status_api_validation_accepts_reviewed_locations(string candidate)
    {
        BhaRacingStatusApiClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/")]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=0")]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=1&secret=1")]
    [InlineData("https://api09.horseracing.software/bha/v1/stewards-room/reports?token=abc")]
    [InlineData("https://www.britishhorseracing.com/racing/results/")]
    public void Racing_status_api_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaRacingStatusApiClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/results/")]
    [InlineData("https://www.britishhorseracing.com/racing/stewards-reports/")]
    [InlineData("https://www.britishhorseracing.com/racing/racing-updates/")]
    public void Racing_status_page_validation_accepts_reviewed_locations(string candidate)
    {
        BhaRacingStatusPageClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/results/?date=2026-10-05")]
    [InlineData("https://www.britishhorseracing.com/racing/fixtures/upcoming/")]
    [InlineData("http://www.britishhorseracing.com/racing/results/")]
    public void Racing_status_page_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaRacingStatusPageClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Fact]
    public void Stewards_reports_uri_is_part_of_default_status_sources()
    {
        BhaRacingStatusApiClient.ValidateSourceUri(StewardsReportsApiUri);
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
