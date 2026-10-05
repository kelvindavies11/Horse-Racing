using System.Net.Http.Headers;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaOwnerApiClient(
    HttpClient httpClient,
    IOptions<BhaCollectionOptions> options) : IRawSourceClient
{
    private static readonly Uri OwnerPageUri =
        new("https://www.britishhorseracing.com/racing/participants/owners/");

    private static readonly HashSet<string> ChampionshipQueryKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "type",
            "page",
            "per_page",
            "sort"
        };

    private readonly BhaCollectionOptions _options = options.Value;

    public async Task<RawSourceResponse> GetAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateSourceUri(sourceUri);
        var sourceOptions = GetSourceOptions(sourceUri);

        var bearerToken = BhaBearerToken.Normalize(_options.OwnerApiBearerToken);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            throw new InvalidOperationException(
                "The BHA owner API sources require an operator-supplied bearer token. " +
                "Configure BhaCollection:OwnerApiBearerToken outside source control.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Referrer = OwnerPageUri;
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
                $"The BHA owner API response declared {declaredLength} bytes, " +
                $"exceeding the configured limit of {sourceOptions.MaximumResponseBytes} bytes.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            sourceOptions.MaximumResponseBytes,
            "BHA owner API",
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
            || !string.Equals(
                sourceUri.AbsolutePath.TrimEnd('/'),
                "/bha/v1/championships/owners",
                StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(sourceUri.Fragment)
            || !string.IsNullOrEmpty(sourceUri.UserInfo)
            || !IsReviewedOwnerQuery(sourceUri.Query))
        {
            throw new InvalidOperationException(
                "This collector is restricted to reviewed BHA owner API sources.");
        }
    }

    private BhaRawSourceOptions GetSourceOptions(Uri sourceUri) =>
        _options.OwnerApiSources.Single(
            source => source.Enabled
                && Uri.TryCreate(source.SourceUrl, UriKind.Absolute, out var configuredUri)
                && Uri.Compare(
                    configuredUri,
                    sourceUri,
                    UriComponents.SchemeAndServer
                        | UriComponents.PathAndQuery,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0);

    private static bool IsReviewedOwnerQuery(string queryString)
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

    private static bool EqualsIgnoreCase(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
