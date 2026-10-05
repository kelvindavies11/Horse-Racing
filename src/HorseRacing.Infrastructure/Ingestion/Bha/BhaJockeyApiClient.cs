using System.Net.Http.Headers;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaJockeyApiClient(
    HttpClient httpClient,
    IOptions<BhaCollectionOptions> options) : IRawSourceClient
{
    private static readonly Uri JockeyPageUri =
        new("https://www.britishhorseracing.com/racing/participants/jockeys/");

    private static readonly HashSet<string> ChampionshipQueryKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "type",
            "page",
            "per_page",
            "sort"
        };

    private static readonly HashSet<string> JockeyListQueryKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "name",
            "page",
            "per_page"
        };

    private static readonly HashSet<string> MilestonesQueryKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "sortby"
        };

    private static readonly HashSet<string> MilestonesSortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "entryName",
            "totalWins",
            "totalRun",
            "racingToday"
        };

    private readonly BhaCollectionOptions _options = options.Value;

    public async Task<RawSourceResponse> GetAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateSourceUri(sourceUri);
        var sourceOptions = GetSourceOptions(sourceUri);

        var bearerToken = BhaBearerToken.Normalize(_options.JockeyApiBearerToken);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            throw new InvalidOperationException(
                "The BHA jockey API sources require an operator-supplied bearer token. " +
                "Configure BhaCollection:JockeyApiBearerToken outside source control.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Referrer = JockeyPageUri;
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
                $"The BHA jockey API response declared {declaredLength} bytes, " +
                $"exceeding the configured limit of {sourceOptions.MaximumResponseBytes} bytes.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            sourceOptions.MaximumResponseBytes,
            "BHA jockey API",
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
            || !IsReviewedJockeyApi(sourceUri))
        {
            throw new InvalidOperationException(
                "This collector is restricted to reviewed BHA jockey API sources.");
        }
    }

    private BhaRawSourceOptions GetSourceOptions(Uri sourceUri) =>
        _options.JockeyApiSources.Single(
            source => source.Enabled
                && Uri.TryCreate(source.SourceUrl, UriKind.Absolute, out var configuredUri)
                && Uri.Compare(
                    configuredUri,
                    sourceUri,
                    UriComponents.SchemeAndServer
                        | UriComponents.PathAndQuery,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0);

    private static bool IsReviewedJockeyApi(Uri sourceUri)
    {
        var path = sourceUri.AbsolutePath.TrimEnd('/');

        if (string.Equals(
                path,
                "/bha/v1/championships/jockeys",
                StringComparison.OrdinalIgnoreCase))
        {
            return IsReviewedChampionshipQuery(sourceUri.Query);
        }

        if (string.Equals(
                path,
                "/bha/v1/jockeys/milestones",
                StringComparison.OrdinalIgnoreCase))
        {
            return IsReviewedMilestonesQuery(sourceUri.Query);
        }

        if (string.Equals(path, "/bha/v1/jockeys", StringComparison.OrdinalIgnoreCase))
        {
            return IsReviewedJockeyListQuery(sourceUri.Query);
        }

        var segments = sourceUri.AbsolutePath.Trim('/').Split('/');
        return segments.Length == 4
            && EqualsIgnoreCase(segments[0], "bha")
            && EqualsIgnoreCase(segments[1], "v1")
            && EqualsIgnoreCase(segments[2], "jockeys")
            && IsPositiveNumber(segments[3])
            && string.IsNullOrEmpty(sourceUri.Query);
    }

    private static bool IsReviewedChampionshipQuery(string queryString)
    {
        var query = ParseQuery(queryString);

        if (!HasOnlyAllowedQueryKeys(query, ChampionshipQueryKeys))
        {
            return false;
        }

        if (query.TryGetValue("type", out var type)
            && !EqualsIgnoreCase(type, "flat")
            && !EqualsIgnoreCase(type, "jump"))
        {
            return false;
        }

        return HasValidPaging(query)
            && IsValidSort(query.GetValueOrDefault("sort"));
    }

    private static bool IsReviewedJockeyListQuery(string queryString)
    {
        var query = ParseQuery(queryString);

        if (!HasOnlyAllowedQueryKeys(query, JockeyListQueryKeys))
        {
            return false;
        }

        if (query.TryGetValue("name", out var name)
            && name.Trim().Length < 2)
        {
            return false;
        }

        return HasValidPaging(query);
    }

    private static bool IsReviewedMilestonesQuery(string queryString)
    {
        var query = ParseQuery(queryString);

        return HasOnlyAllowedQueryKeys(query, MilestonesQueryKeys)
            && IsValidSort(query.GetValueOrDefault("sortby"), MilestonesSortFields);
    }

    private static bool HasValidPaging(Dictionary<string, string> query)
    {
        if (query.TryGetValue("page", out var page)
            && (!int.TryParse(page, out var pageNumber) || pageNumber < 1))
        {
            return false;
        }

        if (query.TryGetValue("per_page", out var perPage)
            && (!int.TryParse(perPage, out var perPageNumber)
                || perPageNumber is < 1 or > 100))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidSort(string? sort) =>
        string.IsNullOrEmpty(sort) || IsValidSort(sort, allowedFields: null);

    private static bool IsValidSort(
        string? sort,
        HashSet<string>? allowedFields)
    {
        if (string.IsNullOrEmpty(sort))
        {
            return true;
        }

        var parts = sort.Split(':', 2);
        return parts.Length == 2
            && (allowedFields is null || allowedFields.Contains(parts[0]))
            && (EqualsIgnoreCase(parts[1], "asc") || EqualsIgnoreCase(parts[1], "desc"));
    }

    private static bool HasOnlyAllowedQueryKeys(
        Dictionary<string, string> query,
        HashSet<string> allowedKeys)
    {
        foreach (var key in query.Keys)
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

    private static bool IsPositiveNumber(string value) =>
        long.TryParse(value, out var number) && number > 0;

    private static bool EqualsIgnoreCase(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
