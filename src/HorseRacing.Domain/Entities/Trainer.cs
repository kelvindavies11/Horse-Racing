namespace HorseRacing.Domain.Entities;

public sealed class Trainer
{
    private Trainer()
    {
    }

    private Trainer(
        Guid id,
        Guid? stableId,
        string name,
        string? licenceNumber,
        string countryCode)
    {
        Id = id;
        StableId = stableId;
        Name = name;
        LicenceNumber = licenceNumber;
        CountryCode = countryCode;
    }

    public Guid Id { get; private set; }

    public Guid? StableId { get; private set; }

    public Stable? Stable { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? LicenceNumber { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public static Trainer Create(
        Guid? stableId,
        string name,
        string? licenceNumber = null,
        string countryCode = "GB")
    {
        if (stableId == Guid.Empty)
        {
            throw new ArgumentException("Stable identifier is required.", nameof(stableId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return new Trainer(
            Guid.NewGuid(),
            stableId,
            name.Trim(),
            string.IsNullOrWhiteSpace(licenceNumber) ? null : licenceNumber.Trim().ToUpperInvariant(),
            countryCode.Trim().ToUpperInvariant());
    }
}
