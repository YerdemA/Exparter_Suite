using Exparter.Domain.Common;
using Exparter.Domain.Identity;

namespace Exparter.Domain.Tests.Identity;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Rotate_revokes_the_token_and_keeps_the_family()
    {
        var token = RefreshToken.IssueForSignIn(UserId, "hash-1", Now);

        var successor = token.Rotate("hash-2", Now.AddMinutes(5));

        Assert.True(token.IsRevoked);
        Assert.Equal(successor.Id, token.ReplacedByTokenId);
        Assert.Equal(token.FamilyId, successor.FamilyId);
        Assert.Equal(UserId, successor.UserId);
        Assert.True(successor.IsActive(Now.AddMinutes(5)));
    }

    [Fact]
    public void Rotate_is_rejected_for_a_revoked_token()
    {
        var token = RefreshToken.IssueForSignIn(UserId, "hash-1", Now);
        token.Revoke(Now);

        Assert.Throws<DomainException>(() => token.Rotate("hash-2", Now));
    }

    [Fact]
    public void Token_expires_after_30_days()
    {
        var token = RefreshToken.IssueForSignIn(UserId, "hash-1", Now);

        Assert.True(token.IsActive(Now.AddDays(30).AddSeconds(-1)));
        Assert.False(token.IsActive(Now.AddDays(30)));
        Assert.Throws<DomainException>(() => token.Rotate("hash-2", Now.AddDays(30)));
    }

    [Fact]
    public void Sign_ins_start_separate_families()
    {
        var first = RefreshToken.IssueForSignIn(UserId, "hash-1", Now);
        var second = RefreshToken.IssueForSignIn(UserId, "hash-2", Now);

        Assert.NotEqual(first.FamilyId, second.FamilyId);
    }
}

public class OneTimeTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static OneTimeToken Issue(OneTimeTokenPurpose purpose) =>
        OneTimeToken.Issue(Guid.NewGuid(), purpose, "hash", Now);

    [Fact]
    public void Use_succeeds_once()
    {
        var token = Issue(OneTimeTokenPurpose.EmailVerification);

        token.Use(OneTimeTokenPurpose.EmailVerification, Now.AddHours(1));

        var exception = Assert.Throws<DomainException>(
            () => token.Use(OneTimeTokenPurpose.EmailVerification, Now.AddHours(2)));
        Assert.Equal(IdentityErrors.InvalidTokenCode, exception.Code);
    }

    [Theory]
    [InlineData(OneTimeTokenPurpose.EmailVerification, 24)]
    [InlineData(OneTimeTokenPurpose.PasswordReset, 1)]
    public void Token_expires_after_its_lifetime(OneTimeTokenPurpose purpose, int lifetimeHours)
    {
        var token = Issue(purpose);

        Assert.True(token.CanBeUsed(purpose, Now.AddHours(lifetimeHours).AddSeconds(-1)));
        Assert.False(token.CanBeUsed(purpose, Now.AddHours(lifetimeHours)));
        Assert.Throws<DomainException>(() => token.Use(purpose, Now.AddHours(lifetimeHours)));
    }

    [Fact]
    public void Token_cannot_be_used_for_another_purpose()
    {
        var token = Issue(OneTimeTokenPurpose.PasswordReset);

        Assert.Throws<DomainException>(() => token.Use(OneTimeTokenPurpose.EmailVerification, Now));
    }

    [Fact]
    public void Invalidate_makes_an_unused_token_unusable()
    {
        var token = Issue(OneTimeTokenPurpose.EmailVerification);

        token.Invalidate(Now);

        Assert.False(token.CanBeUsed(OneTimeTokenPurpose.EmailVerification, Now));
    }
}
