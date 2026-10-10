using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Application.Ingestion.Results;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed partial class BhaHistoricalResultsApiClient(
    HttpClient httpClient,
    IOptions<BhaCollectionOptions> options) : IRaceResultsRawSourceClient
{
    private static readonly Uri ResultsPageUri =
        new("https://www.britishhorseracing.com/racing/results/");
    private const int MaximumResponseBytes = 20_000_000;

    private readonly BhaCollectionOptions _options = options.Value;

    public async Task<RawSourceResponse> GetAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateSourceUri(sourceUri);

        var bearerToken = BhaBearerToken.Normalize(_options.RacingStatusApiBearerToken);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            throw new InvalidOperationException(
                "Historical BHA result collection requires an operator-supplied bearer token. " +
                "Configure BhaCollection:RacingStatusApiBearerToken outside source control.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Referrer = ResultsPageUri;
        request.Headers.Add("Origin", "https://www.britishhorseracing.com");

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var effectiveUri = response.RequestMessage?.RequestUri ?? sourceUri;
        ValidateSourceUri(effectiveUri);

        if (response.Content.Headers.ContentLength > MaximumResponseBytes)
        {
            throw new InvalidOperationException(
                $"The BHA results response exceeded the {MaximumResponseBytes} byte limit.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            MaximumResponseBytes,
            "BHA historical results API",
            cancellationToken);

        return new RawSourceResponse(
            effectiveUri,
            (int)response.StatusCode,
            response.ReasonPhrase,
            response.Content.Headers.ContentType?.MediaType,
            response.Content.Headers.ContentType?.CharSet,
            response.Headers.ETag?.ToString(),
            response.Content.Headers.LastModified,
            content);
    }

    public static void ValidateSourceUri(Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);

        if (!sourceUri.IsAbsoluteUri
            || sourceUri.Scheme != Uri.UriSchemeHttps
            || !sourceUri.Host.Equals("api09.horseracing.software", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(sourceUri.Fragment)
            || !string.IsNullOrEmpty(sourceUri.UserInfo)
            || !IsReviewedPath(sourceUri))
        {
            throw new InvalidOperationException(
                "This client is restricted to reviewed BHA racecourse and historical result endpoints.");
        }
    }

    private static bool IsReviewedPath(Uri sourceUri)
    {
        var path = sourceUri.AbsolutePath.TrimEnd('/');
        if (path.Equals("/bha/v1/racecourses", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrEmpty(sourceUri.Query);
        }

        if (path.Equals("/bha/v1/fixtures", StringComparison.OrdinalIgnoreCase))
        {
            var query = ParseQuery(sourceUri.Query);
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "resultsAvailable", "fields", "year", "month", "page", "per_page"
            };
            return query.Keys.All(allowed.Contains)
                && (!query.TryGetValue("resultsAvailable", out var resultsAvailable)
                    || resultsAvailable == "1")
                && query.ContainsKey("fields")
                && int.TryParse(query.GetValueOrDefault("year"), out var year)
                && year is >= 2000 and <= 2200
                && int.TryParse(query.GetValueOrDefault("month"), out var month)
                && month is >= 1 and <= 12
                && int.TryParse(query.GetValueOrDefault("page"), out var page)
                && page > 0;
        }

        return string.IsNullOrEmpty(sourceUri.Query)
               && (FixtureRacesPath().IsMatch(path)
                   || FixtureGoingPath().IsMatch(path)
                   || RaceDetailsPath().IsMatch(path)
                   || RaceEntriesPath().IsMatch(path)
                   || RaceResultsPath().IsMatch(path));
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty,
                StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^/bha/v1/fixtures/\d{4}/\d+/races$", RegexOptions.IgnoreCase)]
    private static partial Regex FixtureRacesPath();

    [GeneratedRegex(@"^/bha/v1/fixtures/\d{4}/\d+/going$", RegexOptions.IgnoreCase)]
    private static partial Regex FixtureGoingPath();

    [GeneratedRegex(@"^/bha/v1/races/\d{4}/\d+/\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex RaceDetailsPath();

    [GeneratedRegex(@"^/bha/v1/races/\d{4}/\d+/\d+/entries$", RegexOptions.IgnoreCase)]
    private static partial Regex RaceEntriesPath();

    [GeneratedRegex(@"^/bha/v1/races/\d{4}/\d+/\d+/results$", RegexOptions.IgnoreCase)]
    private static partial Regex RaceResultsPath();
}
