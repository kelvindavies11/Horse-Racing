using System.Text;
using System.Text.Json;
using HorseRacing.Application.Ingestion.Curated;
using HorseRacing.Infrastructure.Ingestion.Curated;

namespace HorseRacing.Infrastructure.UnitTests;

public sealed class BhaCuratedDomainObjectExtractorTests
{
    private static readonly DateTimeOffset RetrievedAtUtc =
        new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Racecourses_api_array_promotes_racecourse_objects()
    {
        var extractor = new BhaCuratedDomainObjectExtractor();

        var result = extractor.Extract(CreatePayload(
            "bha-racecourses-api",
            "https://api09.horseracing.software/bha/v1/racecourses/",
            "[{\"courseId\":1,\"courseName\":\"Ascot\"}]"));

        Assert.Equal(CuratedPromotionOutcome.Succeeded, result.Outcome);
        var candidate = Assert.Single(result.DomainObjects);
        Assert.Equal("BHA", candidate.SourceSystem);
        Assert.Equal("Racecourse", candidate.DomainObjectType);
        Assert.Equal("1", candidate.SourceKey);
        Assert.Equal("Ascot", candidate.DisplayName);
        Assert.Equal(RetrievedAtUtc, candidate.ObservedAtUtc);
        using var sourceData = JsonDocument.Parse(candidate.SourceDataJson);
        Assert.Equal("Racecourse", sourceData.RootElement.GetProperty("domainObjectType").GetString());
        Assert.Equal("Ascot", sourceData.RootElement.GetProperty("foundData").GetProperty("courseName").GetString());
    }

    [Fact]
    public void Racehorse_wrapped_data_promotes_horse_objects()
    {
        var extractor = new BhaCuratedDomainObjectExtractor();

        var result = extractor.Extract(CreatePayload(
            "bha-racehorse-api",
            "https://api09.horseracing.software/bha/v1/racehorses?q=red&page=1&per_page=20",
            "{\"data\":[{\"horseId\":123,\"horseName\":\"Red Example\"}]}"));

        var candidate = Assert.Single(result.DomainObjects);
        Assert.Equal("Horse", candidate.DomainObjectType);
        Assert.Equal("123", candidate.SourceKey);
        Assert.Equal("Red Example", candidate.DisplayName);
    }

    [Theory]
    [InlineData("bha-jockeys-list-api", "https://api09.horseracing.software/bha/v1/jockeys?page=1&per_page=100", "Jockey", "jockeyId", "jockeyName")]
    [InlineData("bha-trainers-list-api", "https://api09.horseracing.software/bha/v1/trainers?page=1&per_page=100", "Trainer", "trainerId", "trainerName")]
    [InlineData("bha-owners-list-flat-api", "https://api09.horseracing.software/bha/v1/championships/owners?type=flat", "Owner", "ownerId", "ownerName")]
    public void Participant_payloads_promote_to_expected_domain_object_type(
        string jobName,
        string sourceUrl,
        string expectedType,
        string idProperty,
        string nameProperty)
    {
        var extractor = new BhaCuratedDomainObjectExtractor();

        var result = extractor.Extract(CreatePayload(
            jobName,
            sourceUrl,
            $$"""{"data":[{"{{idProperty}}":456,"{{nameProperty}}":"Example Name"}]}"""));

        var candidate = Assert.Single(result.DomainObjects);
        Assert.Equal(expectedType, candidate.DomainObjectType);
        Assert.Equal("456", candidate.SourceKey);
        Assert.Equal("Example Name", candidate.DisplayName);
    }

    [Fact]
    public void Html_payload_is_skipped_with_reason()
    {
        var extractor = new BhaCuratedDomainObjectExtractor();

        var result = extractor.Extract(CreatePayload(
            "bha-racecourses-page",
            "https://www.britishhorseracing.com/racing/racecourses/",
            "<html>racecourses</html>",
            mediaType: "text/html"));

        Assert.Equal(CuratedPromotionOutcome.Skipped, result.Outcome);
        Assert.Equal("unsupported_media_type", result.ErrorCode);
        Assert.Empty(result.DomainObjects);
    }

    [Fact]
    public void Invalid_json_payload_fails_with_parse_reason()
    {
        var extractor = new BhaCuratedDomainObjectExtractor();

        var result = extractor.Extract(CreatePayload(
            "bha-racecourses-api",
            "https://api09.horseracing.software/bha/v1/racecourses/",
            "{",
            mediaType: "application/json"));

        Assert.Equal(CuratedPromotionOutcome.Failed, result.Outcome);
        Assert.Equal("invalid_json", result.ErrorCode);
        Assert.Empty(result.DomainObjects);
    }

    private static RawPayloadForPromotion CreatePayload(
        string jobName,
        string sourceUrl,
        string content,
        string mediaType = "application/json") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            jobName,
            "BHA source",
            new Uri(sourceUrl),
            new Uri(sourceUrl),
            RetrievedAtUtc,
            200,
            mediaType,
            "utf-8",
            new string('b', 64),
            Encoding.UTF8.GetBytes(content));
}
