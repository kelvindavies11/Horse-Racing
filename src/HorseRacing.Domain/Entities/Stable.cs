namespace HorseRacing.Domain.Entities;

public sealed class Stable
{
    private Stable()
    {
    }

    private Stable(Guid id, string name, string town, string countryCode)
    {
        Id = id;
        Name = name;
        Town = town;
        CountryCode = countryCode;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Town { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public static Stable Create(string name, string town, string countryCode = "GB")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(town);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return new Stable(
            Guid.NewGuid(),
            name.Trim(),
            town.Trim(),
            countryCode.Trim().ToUpperInvariant());
    }
}
