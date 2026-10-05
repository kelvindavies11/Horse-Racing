using HorseRacing.Domain.Enums;

namespace HorseRacing.Domain.Entities;

public sealed class Owner
{
    private Owner()
    {
    }

    private Owner(Guid id, string displayName, OwnerType type, string countryCode)
    {
        Id = id;
        DisplayName = displayName;
        Type = type;
        CountryCode = countryCode;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public OwnerType Type { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public static Owner Create(
        string displayName,
        OwnerType type,
        string countryCode = "GB")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return new Owner(
            Guid.NewGuid(),
            displayName.Trim(),
            type,
            countryCode.Trim().ToUpperInvariant());
    }
}
