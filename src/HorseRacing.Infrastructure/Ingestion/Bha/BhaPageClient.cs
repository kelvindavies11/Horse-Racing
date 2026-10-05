using System.Net.Http.Headers;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaPageClient(
    HttpClient httpClient,
    IOptions<BhaCollectionOptions> options) : IRawSourceClient
{
    private readonly BhaCollectionOptions _options = options.Value;

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
        if (declaredLength > _options.MaximumResponseBytes)
        {
            throw new InvalidOperationException(
                $"The BHA response declared {declaredLength} bytes, exceeding the configured " +
                $"limit of {_options.MaximumResponseBytes} bytes.");
        }

        var content = await ReadBoundedContentAsync(response.Content, cancellationToken);

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

    internal static void ValidateSourceUri(Uri sourceUri)
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
                "/racing/racecourses",
                StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(sourceUri.Query)
            || !string.IsNullOrEmpty(sourceUri.Fragment)
            || !string.IsNullOrEmpty(sourceUri.UserInfo))
        {
            throw new InvalidOperationException(
                "This collector is restricted to the public HTTPS BHA racecourses page.");
        }
    }

    private async Task<byte[]> ReadBoundedContentAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new MemoryStream();
        var buffer = new byte[81920];

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (destination.Length + read > _options.MaximumResponseBytes)
            {
                throw new InvalidOperationException(
                    $"The BHA response exceeded the configured limit of " +
                    $"{_options.MaximumResponseBytes} bytes.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return destination.ToArray();
    }
}
