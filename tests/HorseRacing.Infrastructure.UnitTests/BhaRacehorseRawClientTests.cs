using System.Net;
using System.Text;
using HorseRacing.Infrastructure.Ingestion.Bha;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class BhaRacehorseRawClientTests
{
    private static readonly Uri RacehorsePageUri =
        new("https://www.britishhorseracing.com/racing/horses/racehorse-search-results/");

    private static readonly Uri RacehorseApiUri =
        new("https://api09.horseracing.software/bha/v1/racehorses?q=red&page=1&per_page=20&rated=1");

    [Fact]
    public async Task Racehorse_api_requires_operator_supplied_bearer_token()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new BhaRacehorseApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                RacehorseApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = RacehorseApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync(RacehorseApiUri, CancellationToken.None));

        Assert.Contains("bearer token", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Racehorse_api_sends_configured_token_to_reviewed_endpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            ReasonPhrase = "OK",
            Content = new StringContent(
                "{\"data\":[{\"horseName\":\"Red Example\"}]}",
                Encoding.UTF8,
                "application/json")
        });
        var client = new BhaRacehorseApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                RacehorseApiBearerToken = "racehorse-token",
                RacehorseApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = RacehorseApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var response = await client.GetAsync(RacehorseApiUri, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.MediaType);
        Assert.Equal(
            "{\"data\":[{\"horseName\":\"Red Example\"}]}",
            Encoding.UTF8.GetString(response.Content));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(RacehorseApiUri, request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("racehorse-token", request.Headers.Authorization?.Parameter);
        Assert.Equal(RacehorsePageUri, request.Headers.Referrer);
        Assert.True(request.Headers.Contains("Origin"));
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red&page=2")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red&per_page=50")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red&rated=0")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red&rated=1")]
    public void Racehorse_api_validation_accepts_reviewed_locations(string candidate)
    {
        BhaRacehorseApiClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=re")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red&page=0")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red&per_page=0")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red&per_page=101")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red&rated=2")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red&token=abc")]
    [InlineData("https://www.britishhorseracing.com/racing/horses/racehorse-search-results/")]
    public void Racehorse_api_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaRacehorseApiClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Fact]
    public void Racehorse_search_page_validation_accepts_reviewed_location()
    {
        BhaRacehorseSearchPageClient.ValidateSourceUri(RacehorsePageUri);
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/horses/racehorse-search-results/?q=red")]
    [InlineData("https://www.britishhorseracing.com/racing/horses/all-jockeys/")]
    [InlineData("https://api09.horseracing.software/bha/v1/racehorses?q=red")]
    [InlineData("http://www.britishhorseracing.com/racing/horses/racehorse-search-results/")]
    public void Racehorse_search_page_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaRacehorseSearchPageClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Fact]
    public void Racehorse_search_page_uri_is_part_of_default_options()
    {
        var options = new BhaCollectionOptions();

        Assert.Equal(RacehorsePageUri.ToString(), options.RacehorseSearchPage.SourceUrl);
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
