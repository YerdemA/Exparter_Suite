using Exparter.Domain.Common;

namespace Exparter.Domain.Profiles;

public static class ProfileErrors
{
    public const string SwapModeRequiredCode = "swap_mode_required";
    public const string CityRequiredCode = "city_required";
    public const string InvalidCityCode = "invalid_city";
    public const string InvalidDisplayNameCode = "invalid_display_name";

    public static DomainException SwapModeRequired() =>
        new(SwapModeRequiredCode, "At least one of online and in-person must be accepted.");

    public static DomainException CityRequired() =>
        new(CityRequiredCode, "A city is required when in-person swaps are accepted.");

    public static DomainException InvalidCity() =>
        new(InvalidCityCode, "The city code is not one of the known cities.");

    public static DomainException InvalidDisplayName() =>
        new(InvalidDisplayNameCode, "The display name must be 2 to 50 characters.");
}

public sealed class Profile
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 50;
    public const int BioMaxLength = 500;

    private Profile()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>Null until the user fills in the profile for the first time.</summary>
    public string? DisplayName { get; private set; }

    public string? Bio { get; private set; }
    public string? AvatarKey { get; private set; }
    public string? City { get; private set; }
    public bool AcceptsOnline { get; private set; }
    public bool AcceptsInPerson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Whether the profile holds what matching needs. An incomplete profile is not shown
    /// to other users and its owner cannot interact with them.
    /// </summary>
    public bool IsComplete => DisplayName is not null && (AcceptsOnline || AcceptsInPerson);

    /// <summary>The blank profile every account starts with.</summary>
    public static Profile CreateEmpty(Guid userId, DateTimeOffset now) =>
        new() { UserId = userId, CreatedAt = now, UpdatedAt = now };

    public void Update(
        string displayName,
        string? bio,
        string? city,
        bool acceptsOnline,
        bool acceptsInPerson,
        DateTimeOffset now)
    {
        var name = displayName.Trim();
        if (name.Length is < DisplayNameMinLength or > DisplayNameMaxLength)
        {
            throw ProfileErrors.InvalidDisplayName();
        }

        if (!acceptsOnline && !acceptsInPerson)
        {
            throw ProfileErrors.SwapModeRequired();
        }

        if (city is not null && !Cities.IsValidCode(city))
        {
            throw ProfileErrors.InvalidCity();
        }

        if (acceptsInPerson && city is null)
        {
            throw ProfileErrors.CityRequired();
        }

        DisplayName = name;
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        City = city;
        AcceptsOnline = acceptsOnline;
        AcceptsInPerson = acceptsInPerson;
        UpdatedAt = now;
    }

    /// <returns>The key of the avatar that was replaced, so the caller can delete the file.</returns>
    public string? SetAvatar(string avatarKey, DateTimeOffset now)
    {
        var previous = AvatarKey;
        AvatarKey = avatarKey;
        UpdatedAt = now;
        return previous;
    }

    /// <returns>The key of the removed avatar, or null if there was none.</returns>
    public string? RemoveAvatar(DateTimeOffset now)
    {
        var previous = AvatarKey;
        if (previous is not null)
        {
            AvatarKey = null;
            UpdatedAt = now;
        }

        return previous;
    }
}
