using System.Net.Http.Headers;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaRacingStatusPageClient(
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
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

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
                $"The BHA racing status page response declared {declaredLength} bytes, " +
                $"exceeding the configured limit of {sourceOptions.MaximumResponseBytes} bytes.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            sourceOptions.MaximumResponseBytes,
            "BHA racing status page",
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

        var allowedPath = sourceUri.AbsolutePath.TrimEnd('/');

        if (!sourceUri.IsAbsoluteUri
            || !string.Equals(sourceUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || !string.Equals(
                sourceUri.Host,
                "www.britishhorseracing.com",
                StringComparison.OrdinalIgnoreCase)
            || (string.Equals(allowedPath, "/racing/results", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    allowedPath,
                    "/racing/stewards-reports",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    allowedPath,
                    "/racing/racing-updates",
                    StringComparison.OrdinalIgnoreCase)) is false
            || !string.IsNullOrEmpty(sourceUri.Query)
            || !string.IsNullOrEmpty(sourceUri.Fragment)
            || !string.IsNullOrEmpty(sourceUri.UserInfo))
        {
            throw new InvalidOperationException(
                "This collector is restricted to reviewed public BHA racing status pages.");
        }
    }

    private BhaRawSourceOptions GetSourceOptions(Uri sourceUri) =>
        _options.RacingStatusPageSources.Single(
            source => source.Enabled
                && Uri.TryCreate(source.SourceUrl, UriKind.Absolute, out var configuredUri)
                && Uri.Compare(
                    configuredUri,
                    sourceUri,
                    UriComponents.SchemeAndServer
                        | UriComponents.Path,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0);
}
