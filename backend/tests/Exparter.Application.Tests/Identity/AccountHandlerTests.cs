using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Domain.Identity;

namespace Exparter.Application.Tests.Identity;

public class GetAccountQueryHandlerTests
{
    private readonly IdentityTestContext _context = new();

    [Fact]
    public async Task Returns_the_account()
    {
        var user = _context.AddUser();

        var account = await new GetAccountQueryHandler(_context.Users)
            .Handle(new GetAccountQuery(user.Id), CancellationToken.None);

        Assert.Equal(new AccountDto(user.Id, "ayse@example.com", "ayse", false, UserRole.User, user.CreatedAt), account);
    }

    [Fact]
    public async Task Fails_for_an_unknown_account()
    {
        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await new GetAccountQueryHandler(_context.Users)
                .Handle(new GetAccountQuery(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(AppErrorKind.NotFound, exception.Kind);
    }
}

public class RequestAccountDeletionCommandHandlerTests
{
    private readonly IdentityTestContext _context = new();

    private RequestAccountDeletionCommandHandler Handler => new(
        _context.Users,
        _context.RefreshTokens,
        _context.Hasher,
        _context.UnitOfWork,
        _context.Clock);

    [Fact]
    public async Task Schedules_deletion_and_signs_out_every_device()
    {
        var user = _context.AddUser();
        _context.AddRefreshToken(user);
        _context.AddRefreshToken(user);

        var result = await Handler.Handle(
            new RequestAccountDeletionCommand(user.Id, IdentityTestContext.Password),
            CancellationToken.None);

        Assert.Equal(UserStatus.PendingDeletion, user.Status);
        Assert.Equal(_context.Clock.GetUtcNow().AddDays(30), result.PermanentDeletionAt);
        Assert.All(_context.RefreshTokens.Items, token => Assert.True(token.IsRevoked));
    }

    [Fact]
    public async Task Requires_the_current_password()
    {
        var user = _context.AddUser();

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await Handler.Handle(
                new RequestAccountDeletionCommand(user.Id, "wrong-password"),
                CancellationToken.None));

        Assert.Equal(AppErrorKind.BadRequest, exception.Kind);
        Assert.Equal("invalid_password", exception.Code);
        Assert.Equal(UserStatus.Active, user.Status);
    }
}
