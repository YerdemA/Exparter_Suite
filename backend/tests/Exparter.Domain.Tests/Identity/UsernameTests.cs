using Exparter.Domain.Common;
using Exparter.Domain.Identity;

namespace Exparter.Domain.Tests.Identity;

public class UsernameTests
{
    [Theory]
    [InlineData("ayse", "ayse")]
    [InlineData("Ayse.Yilmaz", "ayse.yilmaz")]
    [InlineData("  user_01  ", "user_01")]
    [InlineData("a_b", "a_b")]
    [InlineData("ISIK", "isik")]
    public void Parse_normalizes_valid_usernames(string input, string expected) =>
        Assert.Equal(expected, Username.Parse(input));

    [Theory]
    [InlineData("ab")]
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
    [InlineData("Exparter")]
    [InlineData("destek")]
    public void Parse_rejects_reserved_usernames(string input)
    {
        var exception = Assert.Throws<DomainException>(() => Username.Parse(input));
        Assert.Equal(IdentityErrors.UsernameReservedCode, exception.Code);
    }
}
