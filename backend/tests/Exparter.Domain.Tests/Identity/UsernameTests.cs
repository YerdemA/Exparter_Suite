using Exparter.Domain.Common;
using Exparter.Domain.Identity;

namespace Exparter.Domain.Tests.Identity;

public class UsernameTests
{
    [Theory]
    [InlineData("ayse")]
    [InlineData("ayse.yilmaz")]
    [InlineData("user_01")]
    [InlineData("a_b")]
    [InlineData("_a1")]
    public void Parse_accepts_valid_usernames(string input) =>
        Assert.Equal(input, Username.Parse(input));

    [Theory]
    [InlineData("Ayse", "ayse")]
    [InlineData("  ayse.yilmaz  ", "ayse.yilmaz")]
    [InlineData("ISIK", "isik")]
    public void NormalizeForLookup_folds_sign_in_input_to_the_stored_form(string input, string expected) =>
        Assert.Equal(expected, Username.NormalizeForLookup(input));

    [Theory]
    [InlineData("ab")]
    [InlineData("Ayse")]
    [InlineData("AYSE")]
    [InlineData(" ayse")]
    [InlineData(".ayse")]
    [InlineData("ayse.")]
    [InlineData("ay..se")]
    [InlineData("ay se")]
    [InlineData("ayşe")]
    [InlineData("ayse@mail")]
    [InlineData("a234567890123456789012345678901")]
    public void Parse_rejects_malformed_usernames(string input)
    {
        var exception = Assert.Throws<DomainException>(() => Username.Parse(input));
        Assert.Equal(IdentityErrors.InvalidUsernameCode, exception.Code);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("exparter")]
    [InlineData("destek")]
    public void Parse_rejects_reserved_usernames(string input)
    {
        var exception = Assert.Throws<DomainException>(() => Username.Parse(input));
        Assert.Equal(IdentityErrors.UsernameReservedCode, exception.Code);
    }
}
