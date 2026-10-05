using System.Net.Http.Headers;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaRacecardApiClient(
    HttpClient httpClient,
    IOptions<BhaCollectionOptions> options) : IRawSourceClient
{
    private static readonly Uri RacecardPageUri =
        new("https://www.britishhorseracing.com/racing/fixtures/upcoming/racecard/race/");

    private readonly BhaCollectionOptions _options = options.Value;

    public async Task<RawSourceResponse> GetAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateSourceUri(sourceUri);
        var sourceOptions = GetSourceOptions(sourceUri);

        var bearerToken = BhaBearerToken.Normalize(_options.RacecardApiBearerToken);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            throw new InvalidOperationException(
                "The BHA racecard API sources require an operator-supplied bearer token. " +
                "Configure BhaCollection:RacecardApiBearerToken outside source control.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Referrer = RacecardPageUri;
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
                $"The BHA racecard API response declared {declaredLength} bytes, " +
                $"exceeding the configured limit of {sourceOptions.MaximumResponseBytes} bytes.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            sourceOptions.MaximumResponseBytes,
            "BHA racecard API",
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
            || !string.IsNullOrEmpty(sourceUri.Query)
            || !string.IsNullOrEmpty(sourceUri.Fragment)
            || !string.IsNullOrEmpty(sourceUri.UserInfo)
            || !IsReviewedRacecardPath(sourceUri.AbsolutePath))
        {
            throw new InvalidOperationException(
                "This collector is restricted to reviewed BHA racecard API sources.");
        }
    }

    private BhaRawSourceOptions GetSourceOptions(Uri sourceUri) =>
        _options.RacecardApiSources.Single(
            source => source.Enabled
                && Uri.TryCreate(source.SourceUrl, UriKind.Absolute, out var configuredUri)
                && Uri.Compare(
                    configuredUri,
                    sourceUri,
                    UriComponents.SchemeAndServer
                        | UriComponents.PathAndQuery,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0);

    private static bool IsReviewedRacecardPath(string absolutePath)
    {
        var segments = absolutePath.Trim('/').Split('/');

        if (segments.Length is < 6 or > 7
            || !EqualsIgnoreCase(segments[0], "bha")
            || !EqualsIgnoreCase(segments[1], "v1"))
        {
            return false;
        }

        if (EqualsIgnoreCase(segments[2], "fixtures"))
        {
            return segments.Length == 6
                && IsYear(segments[3])
                && IsPositiveNumber(segments[4])
                && (EqualsIgnoreCase(segments[5], "races")
                    || EqualsIgnoreCase(segments[5], "going"));
        }

        if (!EqualsIgnoreCase(segments[2], "races")
            || !IsYear(segments[3])
            || !IsPositiveNumber(segments[4])
            || !IsNonNegativeNumber(segments[5]))
        {
            return false;
        }

        if (segments.Length == 6)
        {
            return true;
        }

        return EqualsIgnoreCase(segments[6], "entries")
            || EqualsIgnoreCase(segments[6], "balloted")
            || EqualsIgnoreCase(segments[6], "results")
            || EqualsIgnoreCase(segments[6], "nominations")
            || EqualsIgnoreCase(segments[6], "trans");
    }

    private static bool IsYear(string value) =>
        int.TryParse(value, out var year) && year is >= 2000 and <= 2100;

    private static bool IsPositiveNumber(string value) =>
        long.TryParse(value, out var number) && number > 0;

    private static bool IsNonNegativeNumber(string value) =>
        int.TryParse(value, out var number) && number >= 0;

    private static bool EqualsIgnoreCase(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
