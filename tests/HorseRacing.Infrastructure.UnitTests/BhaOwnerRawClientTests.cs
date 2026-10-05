using System.Net;
using System.Text;
using HorseRacing.Infrastructure.Ingestion.Bha;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class BhaOwnerRawClientTests
{
    private static readonly Uri OwnerPageUri =
        new("https://www.britishhorseracing.com/racing/participants/owners/");

    private static readonly Uri AllOwnersPageUri =
        new("https://www.britishhorseracing.com/racing/participants/owners/all-owners/");

    private static readonly Uri OwnerApiUri =
        new("https://api09.horseracing.software/bha/v1/championships/owners?type=flat&per_page=5");

    [Fact]
    public async Task Owner_api_requires_operator_supplied_bearer_token()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new BhaOwnerApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                OwnerApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = OwnerApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync(OwnerApiUri, CancellationToken.None));

        Assert.Contains("bearer token", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Owner_api_sends_configured_token_to_reviewed_endpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            ReasonPhrase = "OK",
            Content = new StringContent(
                "{\"data\":[{\"ownerName\":\"Example Owner\"}]}",
                Encoding.UTF8,
                "application/json")
        });
        var client = new BhaOwnerApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                OwnerApiBearerToken = "owner-token",
                OwnerApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = OwnerApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var response = await client.GetAsync(OwnerApiUri, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.MediaType);
        Assert.Equal(
            "{\"data\":[{\"ownerName\":\"Example Owner\"}]}",
            Encoding.UTF8.GetString(response.Content));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(OwnerApiUri, request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("owner-token", request.Headers.Authorization?.Parameter);
        Assert.Equal(OwnerPageUri, request.Headers.Referrer);
        Assert.True(request.Headers.Contains("Origin"));
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/owners?type=flat&per_page=5")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/owners?type=jump&page=2&per_page=20&sort=rank:asc")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/owners")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/owners?type=flat&sort=ownerName:desc&page=1&per_page=100")]
    public void Owner_api_validation_accepts_reviewed_locations(string candidate)
    {
        BhaOwnerApiClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/owners?type=all")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/owners?type=flat&per_page=101")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/owners?type=flat&page=0")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/owners?type=flat&sort=rank")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/owners?type=flat&token=abc")]
    [InlineData("https://api09.horseracing.software/bha/v1/owners")]
    [InlineData("https://www.britishhorseracing.com/racing/participants/owners/")]
    public void Owner_api_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaOwnerApiClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/participants/owners/")]
    [InlineData("https://www.britishhorseracing.com/racing/participants/owners/all-owners/")]
    public void Owner_page_validation_accepts_reviewed_locations(string candidate)
    {
        BhaOwnerPageClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/participants/owners/?type=flat")]
    [InlineData("https://www.britishhorseracing.com/regulation/ownership/")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/owners")]
    [InlineData("http://www.britishhorseracing.com/racing/participants/owners/")]
    public void Owner_page_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaOwnerPageClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Fact]
    public void Owner_pages_are_part_of_default_options()
    {
        var options = new BhaCollectionOptions();

        Assert.Contains(
            options.OwnerPageSources,
            source => source.SourceUrl == OwnerPageUri.ToString());
        Assert.Contains(
            options.OwnerPageSources,
            source => source.SourceUrl == AllOwnersPageUri.ToString());
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
