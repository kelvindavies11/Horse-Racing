using System.Net.Http.Headers;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaRacingStatusApiClient(
    HttpClient httpClient,
    IOptions<BhaCollectionOptions> options) : IRawSourceClient
{
    private static readonly Uri ResultsPageUri =
        new("https://www.britishhorseracing.com/racing/results/");

    private readonly BhaCollectionOptions _options = options.Value;

    public async Task<RawSourceResponse> GetAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateSourceUri(sourceUri);
        var sourceOptions = GetSourceOptions(sourceUri);

        var bearerToken = BhaBearerToken.Normalize(_options.RacingStatusApiBearerToken);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            throw new InvalidOperationException(
                "The BHA racing status API sources require an operator-supplied bearer token. " +
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

        var declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength > sourceOptions.MaximumResponseBytes)
        {
            throw new InvalidOperationException(
                $"The BHA racing status API response declared {declaredLength} bytes, " +
                $"exceeding the configured limit of {sourceOptions.MaximumResponseBytes} bytes.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            sourceOptions.MaximumResponseBytes,
            "BHA racing status API",
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
            || !string.Equals(sourceUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || !string.Equals(
                sourceUri.Host,
                "api09.horseracing.software",
                StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(sourceUri.Fragment)
            || !string.IsNullOrEmpty(sourceUri.UserInfo)
            || IsReviewedRacingStatusApi(sourceUri) is false)
        {
            throw new InvalidOperationException(
                "This collector is restricted to reviewed BHA racing status API sources.");
        }
    }

    private BhaRawSourceOptions GetSourceOptions(Uri sourceUri) =>
        _options.RacingStatusApiSources.Single(
            source => source.Enabled
                && Uri.TryCreate(source.SourceUrl, UriKind.Absolute, out var configuredUri)
                && Uri.Compare(
                    configuredUri,
                    sourceUri,
                    UriComponents.SchemeAndServer
                        | UriComponents.PathAndQuery,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0);

    private static bool IsReviewedRacingStatusApi(Uri sourceUri)
    {
        var path = sourceUri.AbsolutePath.TrimEnd('/');

        if (string.Equals(
                path,
                "/bha/v1/stewards-room/reports",
                StringComparison.OrdinalIgnoreCase))
        {
            return HasOnlyAllowedQueryKeys(
                sourceUri.Query,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "q",
                    "ondate",
                    "todate",
                    "fromdate",
                    "page"
                });
        }

        if (!string.Equals(path, "/bha/v1/fixtures", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = ParseQuery(sourceUri.Query);
        return query.TryGetValue("resultsAvailable", out var resultsAvailable)
            && string.Equals(resultsAvailable, "1", StringComparison.Ordinal)
            && HasOnlyAllowedQueryKeys(
                sourceUri.Query,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "resultsAvailable",
                    "fields",
                    "year",
                    "month",
                    "page",
                    "per_page",
                    "fixtureSession",
                    "weekend",
                    "majorEvent",
                    "ladiesDay",
                    "musicNight",
                    "familyDay",
                    "courseId"
                });
    }

    private static bool HasOnlyAllowedQueryKeys(
        string query,
        IReadOnlySet<string> allowedKeys)
    {
        foreach (var key in ParseQuery(query).Keys)
        {
            if (!allowedKeys.Contains(key))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(query))
        {
            return values;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length == 2
                ? Uri.UnescapeDataString(parts[1])
                : string.Empty;

            values[key] = value;
        }

        return values;
    }
}
