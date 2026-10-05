using System.Net;
using System.Text;
using HorseRacing.Infrastructure.Ingestion.Bha;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class BhaFixturesRawClientTests
{
    private static readonly Uri FixturesPageUri =
        new("https://www.britishhorseracing.com/racing/fixtures/full-year/");

    private static readonly Uri FixturesApiUri =
        new("https://api09.horseracing.software/bha/v1/fixtures?per_page=250");

    private static readonly Uri FixtureCalendarUri =
        new("https://crate.horseracing.software/ics/fixtures?year=2026");

    private static readonly Uri FixtureListDownloadUri =
        new("https://media.britishhorseracing.com/bha/Fixture_List/2027-Fixture-list.xlsx");

    [Fact]
    public async Task Fixtures_api_requires_operator_supplied_bearer_token()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new BhaFixturesApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync(FixturesApiUri, CancellationToken.None));

        Assert.Contains("bearer token", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Fixtures_api_sends_configured_token_to_reviewed_endpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            ReasonPhrase = "OK",
            Content = new StringContent(
                "{\"data\":[{\"courseName\":\"Ascot\"}]}",
                Encoding.UTF8,
                "application/json")
        });
        var client = new BhaFixturesApiClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                FixturesApi = new BhaRawSourceOptions
                {
                    BearerToken = "fixture-token",
                    MaximumResponseBytes = 1_000_000
                }
            }));

        var response = await client.GetAsync(FixturesApiUri, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.MediaType);
        Assert.Equal(
            "{\"data\":[{\"courseName\":\"Ascot\"}]}",
            Encoding.UTF8.GetString(response.Content));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(FixturesApiUri, request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("fixture-token", request.Headers.Authorization?.Parameter);
        Assert.Equal(FixturesPageUri, request.Headers.Referrer);
    }

    [Theory]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures")]
    [InlineData("https://api09.horseracing.software/bha/v1/fixtures?per_page=500")]
    [InlineData("https://api09.horseracing.software/bha/v1/racecourses/")]
    [InlineData("http://api09.horseracing.software/bha/v1/fixtures?per_page=250")]
    public void Fixtures_api_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaFixturesApiClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Theory]
    [InlineData("https://www.britishhorseracing.com/racing/fixtures/full-year/?month=10")]
    [InlineData("https://www.britishhorseracing.com/racing/racecourses/")]
    [InlineData("http://www.britishhorseracing.com/racing/fixtures/full-year/")]
    public void Fixtures_page_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaFixturesPageClient.ValidateSourceUri(new Uri(candidate)));
    }

    [Fact]
    public async Task Fixture_calendar_payload_is_collected_from_configured_source()
    {
        var content = "BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/calendar")
        });
        var client = new BhaFixtureCalendarClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                FixtureCalendarSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = FixtureCalendarUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var response = await client.GetAsync(FixtureCalendarUri, CancellationToken.None);

        Assert.Equal("text/calendar", response.MediaType);
        Assert.Equal(content, Encoding.UTF8.GetString(response.Content));
        Assert.Equal(FixtureCalendarUri, Assert.Single(handler.Requests).RequestUri);
    }

    [Fact]
    public async Task Fixture_list_download_payload_is_collected_from_configured_source()
    {
        var content = Encoding.UTF8.GetBytes("xlsx bytes");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        });
        handler.DefaultContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        var client = new BhaFixtureListDownloadClient(
            new HttpClient(handler),
            Options.Create(new BhaCollectionOptions
            {
                FixtureListDownloadSources =
                [
                    new BhaRawSourceOptions
                    {
                        SourceUrl = FixtureListDownloadUri.ToString(),
                        MaximumResponseBytes = 1_000_000
                    }
                ]
            }));

        var response = await client.GetAsync(FixtureListDownloadUri, CancellationToken.None);

        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.MediaType);
        Assert.Equal(content, response.Content);
        Assert.Equal(FixtureListDownloadUri, Assert.Single(handler.Requests).RequestUri);
    }

    [Theory]
    [InlineData("https://media.britishhorseracing.com/bha/Fixture_List/2027-Fixture-list.zip")]
    [InlineData("https://media.britishhorseracing.com/bha/Other/2027-Fixture-list.xlsx")]
    [InlineData("https://www.britishhorseracing.com/bha/Fixture_List/2027-Fixture-list.xlsx")]
    public void Fixture_list_download_validation_rejects_unreviewed_locations(string candidate)
    {
        Assert.Throws<InvalidOperationException>(
            () => BhaFixtureListDownloadClient.ValidateSourceUri(new Uri(candidate)));
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public string? DefaultContentType { get; set; }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = responseFactory(request);

            if (DefaultContentType is not null)
            {
                response.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(DefaultContentType);
            }

            return Task.FromResult(response);
        }
    }
}
