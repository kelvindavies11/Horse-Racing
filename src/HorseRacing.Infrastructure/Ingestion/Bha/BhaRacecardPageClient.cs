using System.Net.Http.Headers;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaRacecardPageClient(
    HttpClient httpClient,
    IOptions<BhaCollectionOptions> options) : IRawSourceClient
{
    private readonly BhaRawSourceOptions _sourceOptions = options.Value.RacecardPage;

    public async Task<RawSourceResponse> GetAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateSourceUri(sourceUri);

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var effectiveUri = response.RequestMessage?.RequestUri ?? sourceUri;
        ValidateSourceUri(effectiveUri);

        var declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength > _sourceOptions.MaximumResponseBytes)
        {
            throw new InvalidOperationException(
                $"The BHA racecard page response declared {declaredLength} bytes, " +
                $"exceeding the configured limit of {_sourceOptions.MaximumResponseBytes} bytes.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            _sourceOptions.MaximumResponseBytes,
            "BHA racecard page",
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
                "www.britishhorseracing.com",
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                sourceUri.AbsolutePath.TrimEnd('/'),
                "/racing/fixtures/upcoming/racecard/race",
                StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(sourceUri.Query)
            || !string.IsNullOrEmpty(sourceUri.Fragment)
            || !string.IsNullOrEmpty(sourceUri.UserInfo))
        {
            throw new InvalidOperationException(
                "This collector is restricted to the public HTTPS BHA racecard page.");
        }
    }
}
