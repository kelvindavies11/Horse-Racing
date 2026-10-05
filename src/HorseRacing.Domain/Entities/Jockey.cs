namespace HorseRacing.Domain.Entities;

public sealed class Jockey
{
    private Jockey()
    {
    }

    private Jockey(Guid id, string name, string? licenceNumber, string countryCode)
    {
        Id = id;
        Name = name;
        LicenceNumber = licenceNumber;
        CountryCode = countryCode;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? LicenceNumber { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public static Jockey Create(
        string name,
        string? licenceNumber = null,
        string countryCode = "GB")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return new Jockey(
            Guid.NewGuid(),
            name.Trim(),
            string.IsNullOrWhiteSpace(licenceNumber) ? null : licenceNumber.Trim().ToUpperInvariant(),
            countryCode.Trim().ToUpperInvariant());
    }
}
