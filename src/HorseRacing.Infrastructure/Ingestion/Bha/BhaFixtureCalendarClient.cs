using System.Net.Http.Headers;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaFixtureCalendarClient(
    HttpClient httpClient,
    IOptions<BhaCollectionOptions> options) : IRawSourceClient
{
    private readonly BhaCollectionOptions _options = options.Value;

    public async Task<RawSourceResponse> GetAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateSourceUri(sourceUri);
        var sourceOptions = GetSourceOptions(sourceUri);

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/calendar"));

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
                $"The BHA fixture calendar response declared {declaredLength} bytes, " +
                $"exceeding the configured limit of {sourceOptions.MaximumResponseBytes} bytes.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            sourceOptions.MaximumResponseBytes,
            "BHA fixture calendar",
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
                "crate.horseracing.software",
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                sourceUri.AbsolutePath.TrimEnd('/'),
                "/ics/fixtures",
                StringComparison.OrdinalIgnoreCase)
            || !IsReviewedYearQuery(sourceUri.Query)
            || !string.IsNullOrEmpty(sourceUri.Fragment)
            || !string.IsNullOrEmpty(sourceUri.UserInfo))
        {
            throw new InvalidOperationException(
                "This collector is restricted to reviewed BHA fixture calendar sources.");
        }
    }

    private BhaRawSourceOptions GetSourceOptions(Uri sourceUri) =>
        _options.FixtureCalendarSources.Single(
            source => source.Enabled
                && Uri.TryCreate(source.SourceUrl, UriKind.Absolute, out var configuredUri)
                && Uri.Compare(
                    configuredUri,
                    sourceUri,
                    UriComponents.SchemeAndServer
                        | UriComponents.PathAndQuery,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0);

    private static bool IsReviewedYearQuery(string query)
    {
        if (!query.StartsWith("?year=", StringComparison.OrdinalIgnoreCase)
            || query.Length != 10)
        {
            return false;
        }

        return int.TryParse(query.AsSpan(6), out var year)
            && year is >= 2000 and <= 2100;
    }
}
