using Exparter.Domain.Common;
using Exparter.Domain.Identity;

namespace Exparter.Domain.Tests.Identity;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static User NewUser() => User.Register("Ayse@Example.com", "Ayse", "hash", ageConfirmed: true, Now);

    [Fact]
    public void Register_normalizes_email_and_username()
    {
        var user = NewUser();

        Assert.Equal("ayse@example.com", user.Email);
        Assert.Equal("ayse", user.Username);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(UserRole.User, user.Role);
        Assert.False(user.IsEmailVerified);
        Assert.Equal(Now, user.AgeConfirmedAt);
    }

    [Fact]
    public void Register_requires_age_confirmation()
    {
        var exception = Assert.Throws<DomainException>(
            () => User.Register("ayse@example.com", "ayse", "hash", ageConfirmed: false, Now));

        Assert.Equal(IdentityErrors.AgeNotConfirmedCode, exception.Code);
    }

    [Fact]
    public void VerifyEmail_can_happen_only_once()
    {
        var user = NewUser();
        user.VerifyEmail(Now);

        var exception = Assert.Throws<DomainException>(() => user.VerifyEmail(Now));

        Assert.True(user.IsEmailVerified);
        Assert.Equal(IdentityErrors.EmailAlreadyVerifiedCode, exception.Code);
    }

    [Fact]
    public void RequestDeletion_schedules_permanent_deletion_after_30_days()
    {
        var user = NewUser();

        user.RequestDeletion(Now);

        Assert.Equal(UserStatus.PendingDeletion, user.Status);
        Assert.Equal(Now.AddDays(30), user.PermanentDeletionAt);
    }

    [Fact]
    public void RequestDeletion_is_rejected_when_already_pending()
    {
        var user = NewUser();
        user.RequestDeletion(Now);

        var exception = Assert.Throws<DomainException>(() => user.RequestDeletion(Now));

        Assert.Equal(IdentityErrors.InvalidStateCode, exception.Code);
    }

    [Fact]
    public void CancelDeletion_restores_a_pending_account()
    {
        var user = NewUser();
        user.RequestDeletion(Now);

        Assert.True(user.CancelDeletion());
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.PermanentDeletionAt);
    }

    [Fact]
    public void CancelDeletion_does_nothing_for_an_active_account()
    {
        var user = NewUser();

        Assert.False(user.CancelDeletion());
        Assert.Equal(UserStatus.Active, user.Status);
    }
}
