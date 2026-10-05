namespace HorseRacing.Infrastructure.Ingestion.Bha;

internal static class BhaBearerToken
{
    public static string? Normalize(string? bearerToken)
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
