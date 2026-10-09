using Exparter.Domain.Common;
using Exparter.Domain.Identity;

namespace Exparter.Domain.Tests.Identity;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static User NewUser() => User.Register("Ayse@Example.com", "ayse", "hash", ageConfirmed: true, Now);

    [Fact]
    public void Register_normalizes_the_email()
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
    public void Register_rejects_an_uppercase_username()
    {
        var exception = Assert.Throws<DomainException>(
            () => User.Register("ayse@example.com", "Ayse", "hash", ageConfirmed: true, Now));

        Assert.Equal(IdentityErrors.InvalidUsernameCode, exception.Code);
    }

    [Fact]
    public void ChangeUsername_applies_the_same_rules_as_registration()
    {
        var user = NewUser();

        Assert.Equal(IdentityErrors.InvalidUsernameCode, Assert.Throws<DomainException>(() => user.ChangeUsername("Ayse", Now)).Code);
        Assert.Equal(IdentityErrors.UsernameReservedCode, Assert.Throws<DomainException>(() => user.ChangeUsername("admin", Now)).Code);
        Assert.Equal("ayse", user.Username);
        Assert.Null(user.UsernameChangeAvailableAt);
    }

    [Fact]
    public void ChangeUsername_is_allowed_right_after_registration_and_then_once_a_week()
    {
        var user = NewUser();

        user.ChangeUsername("ayse.yilmaz", Now);

        Assert.Equal("ayse.yilmaz", user.Username);
        Assert.Equal(Now.AddDays(7), user.UsernameChangeAvailableAt);
        var exception = Assert.Throws<DomainException>(() => user.ChangeUsername("ayse.y", Now.AddDays(7).AddSeconds(-1)));
        Assert.Equal(IdentityErrors.UsernameChangeTooSoonCode, exception.Code);
        Assert.Equal("ayse.yilmaz", user.Username);

        user.ChangeUsername("ayse.y", Now.AddDays(7));

        Assert.Equal("ayse.y", user.Username);
        Assert.Equal(Now.AddDays(14), user.UsernameChangeAvailableAt);
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
