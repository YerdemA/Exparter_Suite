using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Domain.Common;
using Exparter.Domain.Identity;

namespace Exparter.Application.Tests.Identity;

public class EmailVerificationHandlerTests
{
    private readonly IdentityTestContext _context = new();

    private RequestEmailVerificationCommandHandler RequestHandler => new(
        _context.Users,
        _context.OneTimeTokens,
        _context.Tokens,
        _context.Emails,
        _context.UnitOfWork,
        _context.Clock);

    private ConfirmEmailVerificationCommandHandler ConfirmHandler => new(
        _context.Users,
        _context.OneTimeTokens,
        _context.Tokens,
        _context.UnitOfWork,
        _context.Clock);

    private async Task AssertInvalidToken(string token)
    {
        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await ConfirmHandler.Handle(new ConfirmEmailVerificationCommand(token), CancellationToken.None));
        Assert.Equal(AppErrorKind.BadRequest, exception.Kind);
        Assert.Equal("invalid_token", exception.Code);
    }

    [Fact]
    public async Task Request_sends_a_new_token_and_invalidates_the_previous_one()
    {
        var user = _context.AddUser();
        var previous = _context.AddOneTimeToken(user, OneTimeTokenPurpose.EmailVerification);

        await RequestHandler.Handle(new RequestEmailVerificationCommand(user.Id), CancellationToken.None);

        var sent = Assert.Single(_context.Emails.Verifications);
        Assert.NotEqual(previous, sent.Token);
        await AssertInvalidToken(previous);
        await ConfirmHandler.Handle(new ConfirmEmailVerificationCommand(sent.Token), CancellationToken.None);
        Assert.True(user.IsEmailVerified);
    }

    [Fact]
    public async Task Request_is_rejected_when_the_email_is_already_verified()
    {
        var user = _context.AddUser();
        user.VerifyEmail(_context.Clock.GetUtcNow());

        var exception = await Assert.ThrowsAsync<DomainException>(
            async () => await RequestHandler.Handle(new RequestEmailVerificationCommand(user.Id), CancellationToken.None));

        Assert.Equal(IdentityErrors.EmailAlreadyVerifiedCode, exception.Code);
        Assert.Empty(_context.Emails.Verifications);
    }

    [Fact]
    public async Task Confirm_verifies_the_email_once()
    {
        var user = _context.AddUser();
        var token = _context.AddOneTimeToken(user, OneTimeTokenPurpose.EmailVerification);

        await ConfirmHandler.Handle(new ConfirmEmailVerificationCommand(token), CancellationToken.None);

        Assert.True(user.IsEmailVerified);
        await AssertInvalidToken(token);
    }

    [Fact]
    public async Task Confirm_rejects_an_unknown_token() => await AssertInvalidToken("never-issued");

    [Fact]
    public async Task Confirm_rejects_an_expired_token()
    {
        var token = _context.AddOneTimeToken(_context.AddUser(), OneTimeTokenPurpose.EmailVerification);
        _context.Clock.Advance(TimeSpan.FromHours(24));

        await AssertInvalidToken(token);
    }

    [Fact]
    public async Task Confirm_rejects_a_password_reset_token()
    {
        var user = _context.AddUser();

        await AssertInvalidToken(_context.AddOneTimeToken(user, OneTimeTokenPurpose.PasswordReset));
        Assert.False(user.IsEmailVerified);
    }
}

public class PasswordResetHandlerTests
{
    private const string NewPassword = "N3w-Passw0rd";

    private readonly IdentityTestContext _context = new();

    private RequestPasswordResetCommandHandler RequestHandler => new(
        _context.Users,
        _context.OneTimeTokens,
        _context.Tokens,
        _context.Emails,
        _context.UnitOfWork,
        _context.Clock);

    private ConfirmPasswordResetCommandHandler ConfirmHandler => new(
        _context.Users,
        _context.OneTimeTokens,
        _context.RefreshTokens,
        _context.Tokens,
        _context.Hasher,
        _context.UnitOfWork,
        _context.Clock);

    [Fact]
    public async Task Request_sends_a_reset_email_to_a_known_account()
    {
        _context.AddUser();

        await RequestHandler.Handle(new RequestPasswordResetCommand("AYSE@example.com"), CancellationToken.None);

        Assert.Equal("ayse@example.com", Assert.Single(_context.Emails.PasswordResets).Email);
    }

    [Fact]
    public async Task Request_succeeds_silently_for_an_unknown_email()
    {
        await RequestHandler.Handle(new RequestPasswordResetCommand("nobody@example.com"), CancellationToken.None);

        Assert.Empty(_context.Emails.PasswordResets);
        Assert.Empty(_context.OneTimeTokens.Items);
    }

    [Fact]
    public async Task Request_succeeds_silently_for_a_suspended_account()
    {
        IdentityTestContext.Suspend(_context.AddUser());

        await RequestHandler.Handle(new RequestPasswordResetCommand("ayse@example.com"), CancellationToken.None);

        Assert.Empty(_context.Emails.PasswordResets);
    }

    [Fact]
    public async Task Confirm_changes_the_password_and_signs_out_every_device()
    {
        var user = _context.AddUser();
        _context.AddRefreshToken(user);
        _context.AddRefreshToken(user);
        var token = _context.AddOneTimeToken(user, OneTimeTokenPurpose.PasswordReset);

        await ConfirmHandler.Handle(new ConfirmPasswordResetCommand(token, NewPassword), CancellationToken.None);

        Assert.True(_context.Hasher.Verify(user.PasswordHash, NewPassword));
        Assert.False(_context.Hasher.Verify(user.PasswordHash, IdentityTestContext.Password));
        Assert.All(_context.RefreshTokens.Items, refreshToken => Assert.True(refreshToken.IsRevoked));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirm_rejects_an_invalid_token(bool expired)
    {
        var user = _context.AddUser();
        var token = expired ? _context.AddOneTimeToken(user, OneTimeTokenPurpose.PasswordReset) : "never-issued";
        _context.Clock.Advance(TimeSpan.FromHours(1));

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await ConfirmHandler.Handle(new ConfirmPasswordResetCommand(token, NewPassword), CancellationToken.None));

        Assert.Equal("invalid_token", exception.Code);
        Assert.True(_context.Hasher.Verify(user.PasswordHash, IdentityTestContext.Password));
    }
}
