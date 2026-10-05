using System.Net.Http.Headers;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaTrainerApiClient(
    HttpClient httpClient,
    IOptions<BhaCollectionOptions> options) : IRawSourceClient
{
    private static readonly Uri TrainerPageUri =
        new("https://www.britishhorseracing.com/racing/participants/trainers/");

    private static readonly HashSet<string> ChampionshipQueryKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "type",
            "page",
            "per_page",
            "sort"
        };

    private static readonly HashSet<string> TrainerListQueryKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "name",
            "page",
            "per_page"
        };

    private static readonly HashSet<string> TrainerTypedQueryKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "type",
            "page",
            "per_page"
        };

    private readonly BhaCollectionOptions _options = options.Value;

    public async Task<RawSourceResponse> GetAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateSourceUri(sourceUri);
        var sourceOptions = GetSourceOptions(sourceUri);

        var bearerToken = BhaBearerToken.Normalize(_options.TrainerApiBearerToken);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            throw new InvalidOperationException(
                "The BHA trainer API sources require an operator-supplied bearer token. " +
                "Configure BhaCollection:TrainerApiBearerToken outside source control.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Referrer = TrainerPageUri;
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
                $"The BHA trainer API response declared {declaredLength} bytes, " +
                $"exceeding the configured limit of {sourceOptions.MaximumResponseBytes} bytes.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            sourceOptions.MaximumResponseBytes,
            "BHA trainer API",
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
            || !IsReviewedTrainerApi(sourceUri))
        {
            throw new InvalidOperationException(
                "This collector is restricted to reviewed BHA trainer API sources.");
        }
    }

    private BhaRawSourceOptions GetSourceOptions(Uri sourceUri) =>
        _options.TrainerApiSources.Single(
            source => source.Enabled
                && Uri.TryCreate(source.SourceUrl, UriKind.Absolute, out var configuredUri)
                && Uri.Compare(
                    configuredUri,
                    sourceUri,
                    UriComponents.SchemeAndServer
                        | UriComponents.PathAndQuery,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0);

    private static bool IsReviewedTrainerApi(Uri sourceUri)
    {
        var path = sourceUri.AbsolutePath.TrimEnd('/');

        if (string.Equals(
                path,
                "/bha/v1/championships/trainers",
                StringComparison.OrdinalIgnoreCase))
        {
            return IsReviewedChampionshipQuery(sourceUri.Query);
        }

        if (string.Equals(path, "/bha/v1/trainers", StringComparison.OrdinalIgnoreCase))
        {
            return IsReviewedTrainerListQuery(sourceUri.Query);
        }

        if (string.Equals(
                path,
                "/bha/v1/trainers/nonrunners",
                StringComparison.OrdinalIgnoreCase))
        {
            return IsReviewedTypedTrainerQuery(sourceUri.Query);
        }

        var segments = sourceUri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length is < 4 or > 5
            || !EqualsIgnoreCase(segments[0], "bha")
            || !EqualsIgnoreCase(segments[1], "v1")
            || !EqualsIgnoreCase(segments[2], "trainers")
            || !IsPositiveNumber(segments[3]))
        {
            return false;
        }

        if (segments.Length == 4)
        {
            return string.IsNullOrEmpty(sourceUri.Query);
        }

        return (EqualsIgnoreCase(segments[4], "performances")
                || EqualsIgnoreCase(segments[4], "nonrunners"))
            && IsReviewedPagedQuery(sourceUri.Query);
    }

    private static bool IsReviewedChampionshipQuery(string queryString)
    {
        var query = ParseQuery(queryString);

        if (!HasOnlyAllowedQueryKeys(query, ChampionshipQueryKeys))
        {
            return false;
        }

        if (query.TryGetValue("type", out var type) && !IsRaceType(type))
        {
            return false;
        }

        return HasValidPaging(query)
            && IsValidSort(query.GetValueOrDefault("sort"));
    }

    private static bool IsReviewedTrainerListQuery(string queryString)
    {
        var query = ParseQuery(queryString);

        if (!HasOnlyAllowedQueryKeys(query, TrainerListQueryKeys))
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

    private static bool IsReviewedTypedTrainerQuery(string queryString)
    {
        var query = ParseQuery(queryString);

        return HasOnlyAllowedQueryKeys(query, TrainerTypedQueryKeys)
            && query.TryGetValue("type", out var type)
            && IsRaceType(type)
            && HasValidPaging(query);
    }

    private static bool IsReviewedPagedQuery(string queryString)
    {
        var query = ParseQuery(queryString);

        return HasOnlyAllowedQueryKeys(query, TrainerTypedQueryKeys)
            && (!query.TryGetValue("type", out var type) || IsRaceType(type))
            && HasValidPaging(query);
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

    private static bool IsValidSort(string? sort)
    {
        if (string.IsNullOrEmpty(sort))
        {
            return true;
        }

        var parts = sort.Split(':', 2);
        return parts.Length == 2
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

    private static bool IsRaceType(string value) =>
        EqualsIgnoreCase(value, "flat") || EqualsIgnoreCase(value, "jump");

    private static bool IsPositiveNumber(string value) =>
        long.TryParse(value, out var number) && number > 0;

    private static bool EqualsIgnoreCase(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
