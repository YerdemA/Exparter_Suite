using Exparter.Application.Common;
using Exparter.Application.Identity;

namespace Exparter.Application.Tests.Identity;

public class RefreshTokenCommandHandlerTests
{
    private readonly IdentityTestContext _context = new();

    private RefreshTokenCommandHandler Handler => new(
        _context.Users,
        _context.RefreshTokens,
        _context.Tokens,
        _context.TokenIssuer,
        _context.UnitOfWork,
        _context.Clock);

    private async Task<AppException> AssertInvalid(string refreshToken)
    {
        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await Handler.Handle(new RefreshTokenCommand(refreshToken), CancellationToken.None));
        Assert.Equal(AppErrorKind.Unauthorized, exception.Kind);
        Assert.Equal("refresh_token_invalid", exception.Code);
        return exception;
    }

    [Fact]
    public async Task Rotates_the_token()
    {
        var user = _context.AddUser();
        var original = _context.AddRefreshToken(user);

        var result = await Handler.Handle(new RefreshTokenCommand(original), CancellationToken.None);

        Assert.NotEqual(original, result.RefreshToken);
        Assert.Equal(2, _context.RefreshTokens.Items.Count);
        Assert.True(_context.RefreshTokens.Items[0].IsRevoked);
        Assert.False(_context.RefreshTokens.Items[1].IsRevoked);
        Assert.Equal(_context.RefreshTokens.Items[0].FamilyId, _context.RefreshTokens.Items[1].FamilyId);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family()
    {
        var user = _context.AddUser();
        var original = _context.AddRefreshToken(user);
        var otherSignIn = _context.AddRefreshToken(user);
        var rotated = await Handler.Handle(new RefreshTokenCommand(original), CancellationToken.None);

        await AssertInvalid(original);

        // The successor is dead too, but an unrelated sign-in keeps working.
        await AssertInvalid(rotated.RefreshToken);
        await Handler.Handle(new RefreshTokenCommand(otherSignIn), CancellationToken.None);
    }

    [Fact]
    public async Task Rejects_an_unknown_token() => await AssertInvalid("never-issued");

    [Fact]
    public async Task Rejects_an_expired_token()
    {
        var token = _context.AddRefreshToken(_context.AddUser());
        _context.Clock.Advance(TimeSpan.FromDays(30));

        await AssertInvalid(token);
    }

    [Fact]
    public async Task Rejects_a_token_of_a_suspended_account()
    {
        var user = _context.AddUser();
        var token = _context.AddRefreshToken(user);
        IdentityTestContext.Suspend(user);

        await AssertInvalid(token);
    }
}

public class LogoutCommandHandlerTests
{
    private readonly IdentityTestContext _context = new();

    private LogoutCommandHandler Handler =>
        new(_context.RefreshTokens, _context.Tokens, _context.UnitOfWork, _context.Clock);

    [Fact]
    public async Task Revokes_the_token()
    {
        var token = _context.AddRefreshToken(_context.AddUser());

        await Handler.Handle(new LogoutCommand(token), CancellationToken.None);

        Assert.True(Assert.Single(_context.RefreshTokens.Items).IsRevoked);
    }

    [Fact]
    public async Task Succeeds_silently_for_an_unknown_token()
    {
        await Handler.Handle(new LogoutCommand("never-issued"), CancellationToken.None);

        Assert.Equal(0, _context.UnitOfWork.SaveCount);
    }
}
