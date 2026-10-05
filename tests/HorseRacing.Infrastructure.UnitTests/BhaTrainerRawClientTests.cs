using System.Net;
using System.Text;
using HorseRacing.Infrastructure.Ingestion.Bha;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class BhaTrainerRawClientTests
{
    private static readonly Uri TrainerPageUri =
        new("https://www.britishhorseracing.com/racing/participants/trainers/");

    private static readonly Uri TrainerMapPageUri =
        new("https://www.britishhorseracing.com/racing/participants/trainers/trainers-map/");

    private static readonly Uri TrainerNonRunnersPageUri =
        new("https://www.britishhorseracing.com/racing/participants/trainers/trainers-non-runners/");

    private static readonly Uri TrainerApiUri =
        new("https://api09.horseracing.software/bha/v1/championships/trainers?type=flat&per_page=5");

    [Fact]
    public async Task Trainer_api_requires_operator_supplied_bearer_token()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new BhaTrainerApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                TrainerApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = TrainerApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync(TrainerApiUri, CancellationToken.None));

        Assert.Contains("bearer token", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Trainer_api_sends_configured_token_to_reviewed_endpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            ReasonPhrase = "OK",
            Content = new StringContent(
                "{\"data\":[{\"trainerName\":\"Example Trainer\"}]}",
                Encoding.UTF8,
                "application/json")
        });
        var client = new BhaTrainerApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                TrainerApiBearerToken = "trainer-token",
                TrainerApiSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = TrainerApiUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var response = await client.GetAsync(TrainerApiUri, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.MediaType);
        Assert.Equal(
            "{\"data\":[{\"trainerName\":\"Example Trainer\"}]}",
            Encoding.UTF8.GetString(response.Content));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(TrainerApiUri, request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("trainer-token", request.Headers.Authorization?.Parameter);
        Assert.Equal(TrainerPageUri, request.Headers.Referrer);
        Assert.True(request.Headers.Contains("Origin"));
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/trainers?type=flat&per_page=5")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/trainers?type=jump&page=2&per_page=20&sort=rank:asc")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/trainers")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers?page=1&per_page=100")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers?name=smith&page=1&per_page=10")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers/nonrunners?type=flat")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers/12345")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers/12345/performances?page=2")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers/12345/nonrunners?page=1")]
    public void Trainer_api_validation_accepts_reviewed_locations(string candidate)
    {
        BhaTrainerApiClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/trainers?type=all")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/trainers?type=flat&per_page=101")]
    [InlineData("https://api09.horseracing.software/bha/v1/championships/trainers?type=flat&sort=rank")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers?name=s")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers?page=0")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers/nonrunners")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers/nonrunners?type=all")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers/abc")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers/12345/races")]
    [InlineData("https://www.britishhorseracing.com/racing/participants/trainers/")]
    public void Trainer_api_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaTrainerApiClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/participants/trainers/")]
    [InlineData("https://www.britishhorseracing.com/racing/participants/trainers/trainers-map/")]
    [InlineData("https://www.britishhorseracing.com/racing/participants/trainers/trainers-non-runners/")]
    public void Trainer_page_validation_accepts_reviewed_locations(string candidate)
    {
        BhaTrainerPageClient.ValidateSourceUri(new Uri(candidate));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/participants/trainers/?type=flat")]
    [InlineData("https://www.britishhorseracing.com/racing/participants/jockeys/")]
    [InlineData("https://api09.horseracing.software/bha/v1/trainers")]
    [InlineData("http://www.britishhorseracing.com/racing/participants/trainers/")]
    public void Trainer_page_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaTrainerPageClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Fact]
    public void Trainer_pages_are_part_of_default_options()
    {
        var options = new BhaCollectionOptions();

        Assert.Contains(
            options.TrainerPageSources,
            source => source.SourceUrl == TrainerPageUri.ToString());
        Assert.Contains(
            options.TrainerPageSources,
            source => source.SourceUrl == TrainerMapPageUri.ToString());
        Assert.Contains(
            options.TrainerPageSources,
            source => source.SourceUrl == TrainerNonRunnersPageUri.ToString());
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
