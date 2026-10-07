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

public class ChangeUsernameCommandHandlerTests
{
    private readonly IdentityTestContext _context = new();

    private ChangeUsernameCommandHandler Handler => new(_context.Users, _context.UnitOfWork);

    [Fact]
    public async Task Changes_the_username()
    {
        var user = _context.AddUser();

        var account = await Handler.Handle(new ChangeUsernameCommand(user.Id, "ayse.yilmaz"), CancellationToken.None);

        Assert.Equal("ayse.yilmaz", account.Username);
        Assert.Equal("ayse.yilmaz", user.Username);
        Assert.Equal(1, _context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Keeping_the_current_username_is_a_no_op()
    {
        var user = _context.AddUser();

        var account = await Handler.Handle(new ChangeUsernameCommand(user.Id, "ayse"), CancellationToken.None);

        Assert.Equal("ayse", account.Username);
        Assert.Equal(0, _context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Rejects_a_username_taken_by_someone_else()
    {
        var user = _context.AddUser();
        _context.AddUser(email: "other@example.com", username: "taken");

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await Handler.Handle(new ChangeUsernameCommand(user.Id, "taken"), CancellationToken.None));

        Assert.Equal("username_already_taken", exception.Code);
        Assert.Equal("ayse", user.Username);
    }

    [Theory]
    [InlineData("Ayse")]
    [InlineData("a")]
    [InlineData("")]
    public void Validator_rejects_malformed_usernames(string username) =>
        Assert.False(new ChangeUsernameCommandValidator().Validate(new ChangeUsernameCommand(Guid.NewGuid(), username)).IsValid);
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
