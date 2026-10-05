using System.Net;
using System.Text;
using HorseRacing.Infrastructure.Ingestion.Bha;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class BhaRacecoursesApiClientTests
{
    private static readonly Uri SourceUri =
        new("https://api09.horseracing.software/bha/v1/racecourses/");

    [Fact]
    public async Task Missing_bearer_token_fails_before_http_request()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync(SourceUri, CancellationToken.None));

        Assert.Contains("bearer token", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Configured_token_is_sent_to_reviewed_json_endpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            ReasonPhrase = "OK",
            Content = new StringContent(
                "[{\"name\":\"Ascot\"}]",
                Encoding.UTF8,
                "application/json")
        });
        var client = CreateClient(handler, "Bearer configured-token");

        var response = await client.GetAsync(SourceUri, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.MediaType);
        Assert.Equal(SourceUri, response.EffectiveUri);
        Assert.Equal("[{\"name\":\"Ascot\"}]", Encoding.UTF8.GetString(response.Content));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(SourceUri, request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("configured-token", request.Headers.Authorization?.Parameter);
        Assert.Equal(
            new Uri("https://www.britishhorseracing.com/racing/racecourses/"),
            request.Headers.Referrer);
        Assert.Contains(
            "https://www.britishhorseracing.com",
            request.Headers.GetValues("Origin"));
    }

    [Theory]
    [InlineData("http://api09.horseracing.software/bha/v1/racecourses/")]
    [InlineData("https://www.britishhorseracing.com/racing/racecourses/")]
    [InlineData("https://api09.horseracing.software/bha/v1/racecourses/?page=1")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers/")]
    public void Validation_rejects_unreviewed_api_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaRacecoursesApiClient.ValidateSourceUri(new Uri(candidate)));
    }

    private static BhaRacecoursesApiClient CreateClient(
        RecordingHandler handler,
        string? bearerToken = null) =>
        new(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                RacecoursesApi = new BhaRawSourceOptions
                {
                    BearerToken = bearerToken,
                    MaximumResponseBytes = 1_000_000
                }
            }));

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
