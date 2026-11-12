using Exparter.Domain.Common;
using Exparter.Domain.Profiles;

namespace Exparter.Domain.Tests.Profiles;

public class ProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static Profile NewProfile() => Profile.CreateEmpty(Guid.NewGuid(), Now);

    [Fact]
    public void A_new_profile_is_blank_and_incomplete()
    {
        var profile = NewProfile();

        Assert.Null(profile.DisplayName);
        Assert.False(profile.IsComplete);
        Assert.Equal(Now, profile.CreatedAt);
    }

    [Fact]
    public void Update_completes_the_profile_and_trims_text()
    {
        var profile = NewProfile();

        profile.Update("  Ayşe Yılmaz ", "  Gitar çalıyorum. ", "34", Now.AddMinutes(1));

        Assert.Equal("Ayşe Yılmaz", profile.DisplayName);
        Assert.Equal("Gitar çalıyorum.", profile.Bio);
        Assert.Equal("34", profile.City);
        Assert.True(profile.IsComplete);
        Assert.Equal(Now.AddMinutes(1), profile.UpdatedAt);
    }

    [Fact]
    public void Update_stores_a_blank_bio_as_missing()
    {
        var profile = NewProfile();

        profile.Update("Ayşe", "   ", null, Now);

        Assert.Null(profile.Bio);
    }

    [Theory]
    [InlineData("A", null, ProfileErrors.InvalidDisplayNameCode)]
    [InlineData("   ", null, ProfileErrors.InvalidDisplayNameCode)]
    [InlineData("Ayşe", "82", ProfileErrors.InvalidCityCode)]
    [InlineData("Ayşe", "1", ProfileErrors.InvalidCityCode)]
    public void Update_rejects_invalid_input_and_changes_nothing(
        string displayName,
        string? city,
        string expectedCode)
    {
        var profile = NewProfile();

        var exception = Assert.Throws<DomainException>(
            () => profile.Update(displayName, null, city, Now));

        Assert.Equal(expectedCode, exception.Code);
        Assert.False(profile.IsComplete);
    }

    [Fact]
    public void Display_name_may_be_50_characters_but_not_51()
    {
        var profile = NewProfile();

        profile.Update(new string('a', 50), null, null, Now);

        Assert.Throws<DomainException>(
            () => profile.Update(new string('a', 51), null, null, Now));
    }

    [Fact]
    public void SetAvatar_and_RemoveAvatar_report_the_replaced_file()
    {
        var profile = NewProfile();

        Assert.Null(profile.SetAvatar("avatars/a.jpg", Now));
        Assert.Equal("avatars/a.jpg", profile.SetAvatar("avatars/b.jpg", Now));
        Assert.Equal("avatars/b.jpg", profile.RemoveAvatar(Now));
        Assert.Null(profile.AvatarKey);
        Assert.Null(profile.RemoveAvatar(Now));
    }
}

public class CitiesTests
{
    [Fact]
    public void There_are_81_cities_with_consecutive_plate_codes()
    {
        Assert.Equal(81, Cities.All.Count);
        Assert.Equal(
            Enumerable.Range(1, 81).Select(number => number.ToString("00")),
            Cities.All.Select(city => city.Code));
        Assert.Equal(81, Cities.All.Select(city => city.Name).Distinct().Count());
    }

    [Theory]
    [InlineData("01", true)]
    [InlineData("34", true)]
    [InlineData("81", true)]
    [InlineData("00", false)]
    [InlineData("82", false)]
    [InlineData("6", false)]
    public void IsValidCode_accepts_only_known_codes(string code, bool expected) =>
        Assert.Equal(expected, Cities.IsValidCode(code));
}
