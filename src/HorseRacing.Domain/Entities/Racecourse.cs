namespace HorseRacing.Domain.Entities;

public sealed class Racecourse
{
    private Racecourse()
    {
    }

    private Racecourse(Guid id, string name, string countryCode)
    {
        Id = id;
        Name = name;
        CountryCode = countryCode;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public static Racecourse Create(string name, string countryCode = "GB")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return new Racecourse(
            Guid.NewGuid(),
            name.Trim(),
            countryCode.Trim().ToUpperInvariant());
    }
}
