using HorseRacing.Application.Ingestion.Raw;
using HorseRacing.Application.Ingestion.Weather;
using HorseRacing.Infrastructure.Ingestion.Bha;

namespace HorseRacing.Infrastructure.Ingestion.Weather;

public sealed class OpenMeteoRawSourceClient(HttpClient httpClient) : IOpenMeteoRawSourceClient
{
    private const int MaximumResponseBytes = 5_000_000;

    public async Task<RawSourceResponse> GetAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ValidateSourceUri(sourceUri);
        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var effectiveUri = response.RequestMessage?.RequestUri ?? sourceUri;
        ValidateSourceUri(effectiveUri);
        if (response.Content.Headers.ContentLength > MaximumResponseBytes)
        {
            throw new InvalidOperationException(
                $"The Open-Meteo response exceeded the {MaximumResponseBytes} byte limit.");
        }

        var content = await BoundedHttpContentReader.ReadAsync(
            response.Content,
            MaximumResponseBytes,
            "Open-Meteo API",
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
            || sourceUri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(sourceUri.Fragment)
            || !string.IsNullOrEmpty(sourceUri.UserInfo))
        {
            throw new InvalidOperationException("Open-Meteo requests must use an absolute HTTPS API URL.");
        }

        var query = ParseQuery(sourceUri.Query);
        if (sourceUri.Host.Equals("geocoding-api.open-meteo.com", StringComparison.OrdinalIgnoreCase)
            && sourceUri.AbsolutePath.Equals("/v1/search", StringComparison.OrdinalIgnoreCase))
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "name", "count", "language", "format", "countryCode"
            };
            if (query.Keys.All(allowed.Contains)
                && !string.IsNullOrWhiteSpace(query.GetValueOrDefault("name"))
                && query.GetValueOrDefault("countryCode") == "GB")
            {
                return;
            }
        }

        if (sourceUri.Host.Equals("archive-api.open-meteo.com", StringComparison.OrdinalIgnoreCase)
            && sourceUri.AbsolutePath.Equals("/v1/archive", StringComparison.OrdinalIgnoreCase))
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "latitude", "longitude", "start_date", "end_date", "hourly", "timezone", "cell_selection"
            };
            if (query.Keys.All(allowed.Contains)
                && decimal.TryParse(query.GetValueOrDefault("latitude"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var latitude)
                && latitude is >= -90 and <= 90
                && decimal.TryParse(query.GetValueOrDefault("longitude"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var longitude)
                && longitude is >= -180 and <= 180
                && DateOnly.TryParse(query.GetValueOrDefault("start_date"), out var start)
                && DateOnly.TryParse(query.GetValueOrDefault("end_date"), out var end)
                && start == end
                && query.GetValueOrDefault("timezone") == "Europe/London")
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "This client is restricted to reviewed Open-Meteo GB geocoding and historical weather endpoints.");
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty,
                StringComparer.OrdinalIgnoreCase);
}
