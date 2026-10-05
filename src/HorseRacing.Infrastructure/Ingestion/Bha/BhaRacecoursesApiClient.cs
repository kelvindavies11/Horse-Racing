using System.Net.Http.Headers;
using HorseRacing.Application.Ingestion.Raw;
using Microsoft.Extensions.Options;

namespace HorseRacing.Infrastructure.Ingestion.Bha;

public sealed class BhaRacecoursesApiClient(
    HttpClient httpClient,
    IOptions<BhaCollectionOptions> options) : IRawSourceClient
{
    private static readonly Uri RacecoursesPageUri =
        new("https://www.britishhorseracing.com/racing/racecourses/");

    private readonly BhaRawSourceOptions _sourceOptions = options.Value.RacecoursesApi;

    public async Task<RawSourceResponse> GetAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateSourceUri(sourceUri);

        var bearerToken = NormalizeBearerToken(_sourceOptions.BearerToken);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            throw new InvalidOperationException(
                "The BHA racecourses API source requires an operator-supplied bearer token. " +
                "Configure BhaCollection:RacecoursesApi:BearerToken outside source control.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Referrer = RacecoursesPageUri;
        request.Headers.Add("Origin", "https://www.britishhorseracing.com");

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
                $"The BHA racecourses API response declared {declaredLength} bytes, " +
                $"exceeding the configured limit of {_sourceOptions.MaximumResponseBytes} bytes.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            _sourceOptions.MaximumResponseBytes,
            "BHA racecourses API",
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
                "/bha/v1/racecourses",
                StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(sourceUri.Query)
            || !string.IsNullOrEmpty(sourceUri.Fragment)
            || !string.IsNullOrEmpty(sourceUri.UserInfo))
        {
            throw new InvalidOperationException(
                "This collector is restricted to the reviewed BHA racecourses API source.");
        }
    }

    private static string? NormalizeBearerToken(string? bearerToken)
    {
        var token = bearerToken?.Trim();
        const string scheme = "Bearer ";

        if (token is not null
            && token.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            token = token[scheme.Length..].Trim();
        }

        return token;
    }
}
