using System.Net;
using System.Text;
using HorseRacing.Infrastructure.Ingestion.Bha;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class BhaJockeyRawClientTests
{
    private static readonly Uri JockeyPageUri =
        new("https://www.britishhorseracing.com/racing/participants/jockeys/");

    private static readonly Uri JockeyWinnersPageUri =
        new("https://www.britishhorseracing.com/racing/jockeys-winners-totals/");

    private static readonly Uri JockeyApiUri =
        new("https://api09.horseracing.software/bha/v1/championships/jockeys?type=flat&per_page=5");

    [Fact]
    public async Task Jockey_api_requires_operator_supplied_bearer_token()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new BhaJockeyApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                JockeyApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = JockeyApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync(JockeyApiUri, CancellationToken.None));

        Assert.Contains("bearer token", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Jockey_api_sends_configured_token_to_reviewed_endpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            ReasonPhrase = "OK",
            Content = new StringContent(
                "{\"data\":[{\"name\":\"Example Jockey\"}]}",
                Encoding.UTF8,
                "application/json")
        });
        var client = new BhaJockeyApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                JockeyApiBearerToken = "jockey-token",
                JockeyApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = JockeyApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var response = await client.GetAsync(JockeyApiUri, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.MediaType);
        Assert.Equal(
            "{\"data\":[{\"name\":\"Example Jockey\"}]}",
            Encoding.UTF8.GetString(response.Content));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(JockeyApiUri, request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("jockey-token", request.Headers.Authorization?.Parameter);
        Assert.Equal(JockeyPageUri, request.Headers.Referrer);
        Assert.True(request.Headers.Contains("Origin"));
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/jockeys?type=flat&per_page=5")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/jockeys?type=jump&page=2&per_page=20&sort=rank:asc")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/jockeys")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys?page=1&per_page=100")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys?name=smith&page=1&per_page=10")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys/12345")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys/milestones")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys/milestones?sortby=totalWins:desc")]
    public void Jockey_api_validation_accepts_reviewed_locations(string candidate)
    {
        BhaJockeyApiClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/jockeys?type=all")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/jockeys?type=flat&per_page=101")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/jockeys?type=flat&sort=rank")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys?name=s")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys?page=0")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys/abc")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys/milestones?sortby=unknown:asc")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys/milestones?token=abc")]
    [InlineData("https://www.britishhorseracing.com/racing/participants/jockeys/")]
    public void Jockey_api_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaJockeyApiClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/participants/jockeys/")]
    [InlineData("https://www.britishhorseracing.com/racing/jockeys-winners-totals/")]
    public void Jockey_page_validation_accepts_reviewed_locations(string candidate)
    {
        BhaJockeyPageClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/participants/jockeys/?type=flat")]
    [InlineData("https://www.britishhorseracing.com/racing/horses/all-jockeys/")]
    [InlineData("https://api09.horseracing.software/bha/v1/jockeys")]
    [InlineData("http://www.britishhorseracing.com/racing/participants/jockeys/")]
    public void Jockey_page_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaJockeyPageClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Fact]
    public void Jockey_pages_are_part_of_default_options()
    {
        var options = new BhaCollectionOptions();

        Assert.Contains(
            options.JockeyPageSources,
            source => source.SourceUrl == JockeyPageUri.ToString());
        Assert.Contains(
            options.JockeyPageSources,
            source => source.SourceUrl == JockeyWinnersPageUri.ToString());
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
