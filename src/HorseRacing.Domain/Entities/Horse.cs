namespace HorseRacing.Domain.Entities;

public sealed class Horse
{
    private Horse()
    {
    }

    private Horse(Guid id, string name, DateOnly foaledOn, string countryCode)
    {
        Id = id;
        Name = name;
        FoaledOn = foaledOn;
        CountryCode = countryCode;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateOnly FoaledOn { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public static Horse Create(string name, DateOnly foaledOn, string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        if (foaledOn > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentOutOfRangeException(nameof(foaledOn), "Foaling date cannot be in the future.");
        }

        return new Horse(
            Guid.NewGuid(),
            name.Trim(),
            foaledOn,
            countryCode.Trim().ToUpperInvariant());
    }
}
